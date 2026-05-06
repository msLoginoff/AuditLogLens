using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;

namespace AuditLogLens.Enrichment;

internal static class EnrichmentDataLoader
{
    private static readonly MethodInfo _loadEntitiesGenericMethod =
        typeof(EnrichmentDataLoader)
            .GetMethod(nameof(LoadEntitiesGenericAsync), BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException(
            $"Method {nameof(LoadEntitiesGenericAsync)} was not found.");

    private static readonly ConcurrentDictionary<(Type Entity, Type Key), MethodInfo> _closedMethodCache = new();

    public static async Task LoadAsync(
        IReadOnlyCollection<EntityLoadRequest> loadRequests,
        AuditEnrichmentContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(loadRequests);
        ArgumentNullException.ThrowIfNull(context);

        if (loadRequests.Count == 0)
            return;

        var trackedEntries = context.DbContext.ChangeTracker
            .Entries()
            .Where(x => x.State != EntityState.Detached)
            .ToList();

        foreach (var group in loadRequests.GroupBy(r => (r.EntityType, r.PropertyName)))
        {
            var entityType = group.Key.EntityType;
            var propertyName = group.Key.PropertyName;

            var values = group.SelectMany(r => r.Values).Distinct().ToList();
            if (values.Count == 0)
                continue;

            var property = GetRequiredEfProperty(context.DbContext, entityType, propertyName);
            var tracked = GetTrackedEntities(
                trackedEntries,
                entityType,
                propertyName,
                values,
                out var trackedValues);

            var missingValues = values
                .Where(x => !trackedValues.Contains(x))
                .ToList();

            var loaded = missingValues.Count == 0
                ? []
                : await LoadEntitiesByPropertyValuesAsync(
                        context.DbContext, entityType, property, missingValues, cancellationToken)
                    .ConfigureAwait(false);

            context.SetLoadedEntities(
                entityType,
                propertyName,
                Deduplicate(context.DbContext, entityType, tracked.Concat(loaded).ToList()));
        }
    }

    private static Task<IReadOnlyList<object>> LoadEntitiesByPropertyValuesAsync(
        DbContext dbContext,
        Type entityType,
        IProperty property,
        IReadOnlyList<object> values,
        CancellationToken cancellationToken)
    {
        var method = _closedMethodCache.GetOrAdd(
            (entityType, property.ClrType),
            static key => _loadEntitiesGenericMethod.MakeGenericMethod(key.Entity, key.Key));

        return (Task<IReadOnlyList<object>>)(method.Invoke(null, [dbContext, property.Name, values, cancellationToken])
                                             ?? Task.FromResult<IReadOnlyList<object>>(Array.Empty<object>()));
    }

    private static async Task<IReadOnlyList<object>> LoadEntitiesGenericAsync<TEntity, TProperty>(
        DbContext dbContext,
        string propertyName,
        IReadOnlyList<object> rawValues,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        var typedValues = rawValues.Select(x => (TProperty)x).Distinct().ToList();

        if (typedValues.Count == 0)
            return Array.Empty<object>();

        return await dbContext
            .Set<TEntity>()
            .AsNoTracking()
            .Where(x => typedValues.Contains(EF.Property<TProperty>(x, propertyName)))
            .Cast<object>()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static IProperty GetRequiredEfProperty(
        DbContext dbContext,
        Type entityType,
        string propertyName)
    {
        var efEntityType = dbContext.Model.FindEntityType(entityType)
                           ?? throw new InvalidOperationException(
                               $"Entity type {entityType.FullName} is not part of the current DbContext model.");

        return efEntityType.FindProperty(propertyName)
               ?? throw new InvalidOperationException(
                   $"Property '{propertyName}' was not found on entity type {entityType.FullName}.");
    }

    private static IReadOnlyList<object> GetTrackedEntities(
        IReadOnlyList<EntityEntry> trackedEntries,
        Type entityType,
        string propertyName,
        IReadOnlyCollection<object> values,
        out HashSet<object> trackedValues)
    {
        trackedValues = [];

        if (values.Count == 0)
            return [];

        var requestedValues = values.ToHashSet();
        var result = new List<object>();

        foreach (var entry in trackedEntries)
        {
            if (!entityType.IsAssignableFrom(entry.Metadata.ClrType)
                || entry.Metadata.FindProperty(propertyName) is null)
            {
                continue;
            }

            var value = entry.Property(propertyName).CurrentValue;
            if (value is null || !requestedValues.Contains(value))
                continue;

            trackedValues.Add(value);
            result.Add(entry.Entity);
        }

        return result;
    }

    private static IReadOnlyList<object> Deduplicate(
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