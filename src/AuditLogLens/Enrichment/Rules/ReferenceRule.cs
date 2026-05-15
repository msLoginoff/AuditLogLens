using System.Collections.Concurrent;
using System.Reflection;
using AuditLogLens.Enrichment.Context;
using AuditLogLens.Enrichment.Internal.Loading;
using Microsoft.EntityFrameworkCore;

namespace AuditLogLens.Enrichment.Rules;

public sealed class ReferenceRule : EnrichmentRule
{
    private static readonly ConcurrentDictionary<(Type EntityType, string PropertyName), PropertyInfo> _propertyCache =
        new();

    public required Type TargetEntityType { get; init; }

    public required string ForeignKeyPropertyName { get; init; }

    public required string TargetKeyPropertyName { get; init; }

    public required string FieldName { get; init; }

    public required Func<object, object?> ValueSelector { get; init; }

    internal override EntityLoadRequest? BuildLoadRequest(
        IReadOnlyList<AuditChange> changes,
        AuditEnrichmentContext context)
    {
        var values = EnrichmentValueCollector.DistinctNonNull(
            changes.SelectMany(change =>
            {
                var (oldFk, newFk) = ResolveForeignKeys(change);
                return new[] { oldFk, newFk };
            }));

        return values.Count > 0
            ? new EntityLoadRequest
            {
                EntityType = TargetEntityType,
                PropertyName = TargetKeyPropertyName,
                Values = values
            }
            : null;
    }

    internal override void Apply(IReadOnlyList<AuditChange> changes, AuditEnrichmentContext context)
    {
        var loadedEntities = context.GetLoadedEntities(TargetEntityType, TargetKeyPropertyName);
        var entitiesByKey = LoadedEntityLookup.OneByKey(loadedEntities, GetKeyValue);

        foreach (var change in changes)
        {
            var (oldFk, newFk) = ResolveForeignKeys(change);

            if (oldFk is not null)
            {
                if (entitiesByKey.TryGetValue(oldFk, out var oldTarget))
                    context.GetBagForChange(change).SetOld(FieldName, ValueSelector(oldTarget));
            }

            if (newFk is not null)
            {
                if (entitiesByKey.TryGetValue(newFk, out var newTarget))
                    context.GetBagForChange(change).SetNew(FieldName, ValueSelector(newTarget));
            }
        }
    }

    private (object? oldFk, object? newFk) ResolveForeignKeys(AuditChange change)
    {
        var currentFk = change.Entry?.Property(ForeignKeyPropertyName).CurrentValue;

        var hasOld = change.OldValues.TryGetValue(ForeignKeyPropertyName, out var oldFk);
        var hasNew = change.NewValues.TryGetValue(ForeignKeyPropertyName, out var newFk);

        return change.State switch
        {
            nameof(EntityState.Added) => (null, ResolveNewForeignKey(hasNew, newFk, currentFk)),
            nameof(EntityState.Deleted) => (hasOld ? oldFk : currentFk, null),
            nameof(EntityState.Modified) => ResolveModified(hasOld, oldFk, hasNew, newFk, currentFk),
            _ => (null, currentFk)
        };
    }

    private static object? ResolveNewForeignKey(
        bool hasNew,
        object? newFk,
        object? currentFk)
    {
        if (currentFk is not null)
            return currentFk;

        return hasNew ? newFk : currentFk;
    }

    private static (object? oldFk, object? newFk) ResolveModified(
        bool hasOld, object? oldFk,
        bool hasNew, object? newFk,
        object? currentFk)
    {
        if (hasOld || hasNew)
            return (hasOld ? oldFk : currentFk, hasNew ? newFk : currentFk);

        return (null, null);
    }

    private object? GetKeyValue(object entity)
    {
        var entityType = entity.GetType();
        var property = _propertyCache.GetOrAdd(
            (entityType, TargetKeyPropertyName),
            static key => key.EntityType.GetProperty(key.PropertyName, BindingFlags.Public | BindingFlags.Instance)
                          ?? throw new InvalidOperationException(
                              $"Property '{key.PropertyName}' was not found on type {key.EntityType.FullName}."));

        return property.GetValue(entity);
    }
}