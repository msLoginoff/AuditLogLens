using System.Collections.Concurrent;
using System.Reflection;
using AuditLogLens.Detection.Internal;
using AuditLogLens.Enrichment.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;

namespace AuditLogLens.Enrichment.Internal.Loading;

internal static class EntityLoadRequestExecutor
{
    private static readonly MethodInfo _queryEntitiesGenericAsyncMethod =
        typeof(EntityLoadRequestExecutor)
            .GetMethod(nameof(QueryEntitiesByPropertyValuesGenericAsync), BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException(
            $"Method {nameof(QueryEntitiesByPropertyValuesGenericAsync)} was not found.");

    private static readonly ConcurrentDictionary<(Type Entity, Type Key), MethodInfo> _queryMethodCacheByType = new();

    private static readonly ConcurrentDictionary<(IModel Model, Type Entity, string Property), LoadTarget>
        _loadTargetCache = new();

    public static async Task ExecuteAsync(
        IReadOnlyCollection<EntityLoadRequest> loadRequests,
        AuditEnrichmentContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(loadRequests);
        ArgumentNullException.ThrowIfNull(context);

        if (loadRequests.Count == 0)
            return;

        foreach (var group in loadRequests.GroupBy(r => (r.EntityType, r.PropertyName)))
        {
            var entityType = group.Key.EntityType;
            var propertyName = group.Key.PropertyName;

            var values = group.SelectMany(r => r.Values).Distinct().ToList();
            if (values.Count == 0)
                continue;

            var includePaths = group
                .SelectMany(r => r.IncludePaths)
                .Distinct()
                .ToList();

            var target = GetRequiredLoadTarget(context.DbContext, entityType, propertyName);
            var tracked = FindUsableTrackedEntities(
                context.GetTrackedEntriesOf(entityType),
                propertyName,
                values,
                includePaths,
                out var trackedValues);

            var valuesToLoad = target.CanTrackedEntitySatisfyValue
                ? values.Where(x => !trackedValues.Contains(x)).ToList()
                : values;

            var loaded = valuesToLoad.Count == 0
                ? []
                : await QueryEntitiesByPropertyValuesAsync(
                        context.DbContext, entityType, target.Property, valuesToLoad, includePaths, cancellationToken)
                    .ConfigureAwait(false);

            context.SetLoadedEntities(
                entityType,
                propertyName,
                DeduplicateByPrimaryKey(context.DbContext, entityType, tracked.Concat(loaded).ToList()));
        }
    }

    private static Task<IReadOnlyList<object>> QueryEntitiesByPropertyValuesAsync(
        DbContext dbContext,
        Type entityType,
        IProperty property,
        IReadOnlyList<object> values,
        IReadOnlyList<string> includePaths,
        CancellationToken cancellationToken)
    {
        var method = _queryMethodCacheByType.GetOrAdd(
            (entityType, property.ClrType),
            static key => _queryEntitiesGenericAsyncMethod.MakeGenericMethod(key.Entity, key.Key));

        var result = method.Invoke(
            obj: null,
            parameters:
            [
                dbContext,
                property.Name,
                values,
                includePaths,
                cancellationToken
            ]);

        return result as Task<IReadOnlyList<object>>
               ?? throw new InvalidOperationException(
                   $"Method {method.Name} returned an unexpected result.");
    }

    private static async Task<IReadOnlyList<object>> QueryEntitiesByPropertyValuesGenericAsync<TEntity, TProperty>(
        DbContext dbContext,
        string propertyName,
        IReadOnlyList<object> rawValues,
        IReadOnlyList<string> includePaths,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        var typedValues = rawValues.Select(x => (TProperty)x).Distinct().ToList();

        if (typedValues.Count == 0)
            return Array.Empty<object>();

        var query = dbContext
            .Set<TEntity>()
            .AsNoTracking();

        foreach (var includePath in includePaths)
        {
            query = query.Include(includePath);
        }

        return await query
            .Where(x => typedValues.Contains(EF.Property<TProperty>(x, propertyName)))
            .Cast<object>()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static LoadTarget GetRequiredLoadTarget(
        DbContext dbContext,
        Type entityType,
        string propertyName)
    {
        return _loadTargetCache.GetOrAdd(
            (dbContext.Model, entityType, propertyName),
            static key => BuildRequiredLoadTarget(key.Model, key.Entity, key.Property));
    }

    private static LoadTarget BuildRequiredLoadTarget(
        IModel model,
        Type entityType,
        string propertyName)
    {
        var efEntityType = model.FindEntityType(entityType)
                           ?? throw new InvalidOperationException(
                               $"Entity type {entityType.FullName} is not part of the current DbContext model.");

        var property = efEntityType.FindProperty(propertyName)
                       ?? throw new InvalidOperationException(
                           $"Property '{propertyName}' was not found on entity type {entityType.FullName}.");

        return new LoadTarget(
            property,
            IsSingleValueUnique(efEntityType, property));
    }

    private static bool IsSingleValueUnique(
        IEntityType entityType,
        IProperty property)
    {
        return entityType.GetKeys().Any(key =>
                   key.Properties.Count == 1
                   && key.Properties[0] == property)
               || entityType.GetIndexes().Any(index =>
                   index.IsUnique
                   && index.Properties.Count == 1
                   && index.Properties[0] == property);
    }

    private static IReadOnlyList<object> FindUsableTrackedEntities(
        IReadOnlyList<AuditTrackedEntry> trackedEntries,
        string propertyName,
        IReadOnlyCollection<object> values,
        IReadOnlyCollection<string> includePaths,
        out HashSet<object> trackedValues)
    {
        trackedValues = [];

        if (values.Count == 0)
            return [];

        var requestedValues = values.ToHashSet();
        var result = new List<object>();

        foreach (var entry in trackedEntries)
        {
            if (!HasAllRequestedIncludesLoaded(entry.Entry, includePaths))
            {
                continue;
            }

            if (entry.Entry.Metadata.FindProperty(propertyName) is null)
            {
                continue;
            }

            var value = entry.Entry.Property(propertyName).CurrentValue;
            if (value is null || !requestedValues.Contains(value))
                continue;

            trackedValues.Add(value);
            result.Add(entry.Entity);
        }

        return result;
    }

    private static bool HasAllRequestedIncludesLoaded(
        EntityEntry entry,
        IReadOnlyCollection<string> includePaths)
    {
        if (includePaths.Count == 0)
            return true;

        foreach (var includePath in includePaths)
        {
            if (includePath.Contains('.'))
                return false;

            var navigation = entry.Metadata.FindNavigation(includePath);
            if (navigation is null)
                return false;

            var isLoaded = navigation.IsCollection
                ? entry.Collection(includePath).IsLoaded
                : entry.Reference(includePath).IsLoaded;

            if (!isLoaded)
                return false;
        }

        return true;
    }

    private static IReadOnlyList<object> DeduplicateByPrimaryKey(
        DbContext dbContext,
        Type entityType,
        IReadOnlyList<object> entities)
    {
        if (entities.Count <= 1)
            return entities;

        var efEntityType = dbContext.Model.FindEntityType(entityType)
                           ?? throw new InvalidOperationException(
                               $"Entity type {entityType.FullName} is not part of the current DbContext model.");

        var primaryKey = efEntityType.FindPrimaryKey();
        if (primaryKey is null || primaryKey.Properties.Count == 0)
            return entities;

        var seen = new HashSet<EntityKey>();
        var result = new List<object>();

        foreach (var entity in entities)
        {
            var key = EntityKey.From(primaryKey, entity);
            if (key is not null && seen.Add(key))
                result.Add(entity);
        }

        return result;
    }

    private sealed record LoadTarget(
        IProperty Property,
        bool CanTrackedEntitySatisfyValue);

    private sealed class EntityKey : IEquatable<EntityKey>
    {
        private readonly object?[] _values;

        private EntityKey(object?[] values)
        {
            _values = values;
        }

        public static EntityKey? From(IKey primaryKey, object entity)
        {
            var values = new object?[primaryKey.Properties.Count];

            for (var i = 0; i < primaryKey.Properties.Count; i++)
            {
                var property = primaryKey.Properties[i];
                if (property.PropertyInfo is null)
                    return null;

                values[i] = property.PropertyInfo.GetValue(entity);
            }

            return new EntityKey(values);
        }

        public bool Equals(EntityKey? other)
        {
            return other is not null && _values.SequenceEqual(other._values);
        }

        public override bool Equals(object? obj)
        {
            return obj is EntityKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            foreach (var value in _values)
                hash.Add(value);

            return hash.ToHashCode();
        }
    }
}