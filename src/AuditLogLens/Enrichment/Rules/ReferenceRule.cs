using AuditLogLens.Enrichment.Context;
using AuditLogLens.Enrichment.Internal;
using AuditLogLens.Enrichment.Internal.Loading;

namespace AuditLogLens.Enrichment.Rules;

public sealed class ReferenceRule : EnrichmentRule
{
    public required Type TargetEntityType { get; init; }

    public required string ForeignKeyPropertyName { get; init; }

    public required string TargetKeyPropertyName { get; init; }

    public required string FieldName { get; init; }

    public required Func<object, object?> ValueSelector { get; init; }

    public IReadOnlyList<string> IncludePaths { get; init; } = [];

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
                Values = values,
                IncludePaths = IncludePaths
            }
            : null;
    }

    internal override void Apply(IReadOnlyList<AuditChange> changes, AuditEnrichmentContext context)
    {
        var loadedEntities = context.GetLoadedEntitiesOf(TargetEntityType, TargetKeyPropertyName);
        var entitiesByKey = LoadedEntityLookup.OneByKey(loadedEntities, GetKeyValue);

        foreach (var change in changes)
        {
            var (oldFk, newFk) = ResolveForeignKeys(change);

            if (oldFk is not null)
            {
                if (entitiesByKey.TryGetValue(oldFk, out var oldTarget))
                    context.GetBagFor(change).SetOld(FieldName, ValueSelector(oldTarget));
            }

            if (newFk is not null)
            {
                if (entitiesByKey.TryGetValue(newFk, out var newTarget))
                    context.GetBagFor(change).SetNew(FieldName, ValueSelector(newTarget));
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
            AuditChangeState.Added => (null, ResolveNewForeignKey(hasNew, newFk, currentFk)),
            AuditChangeState.Deleted => (hasOld ? oldFk : currentFk, null),
            AuditChangeState.Modified => ResolveModified(hasOld, oldFk, hasNew, newFk, currentFk),
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
        return EntityPropertyReader.GetValue(entity, TargetKeyPropertyName);
    }
}
