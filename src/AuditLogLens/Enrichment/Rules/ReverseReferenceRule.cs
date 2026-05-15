using AuditLogLens.Enrichment.Context;
using AuditLogLens.Enrichment.Internal.Loading;

namespace AuditLogLens.Enrichment.Rules;

public sealed class ReverseReferenceRule : EnrichmentRule
{
    public required Type TargetEntityType { get; init; }

    public required Func<AuditChange, object?> SourceKeySelector { get; init; }

    public required string TargetForeignKeyPropertyName { get; init; }

    public required Func<object, object?> TargetForeignKeySelector { get; init; }

    public required Action<AuditChange, IReadOnlyList<object>, AuditEnrichmentBag> Map { get; init; }

    internal override EntityLoadRequest? BuildLoadRequest(
        IReadOnlyList<AuditChange> changes,
        AuditEnrichmentContext context)
    {
        var values = EnrichmentValueCollector.DistinctNonNull(
            changes.Select(SourceKeySelector));

        return values.Count > 0
            ? new EntityLoadRequest
            {
                EntityType = TargetEntityType,
                PropertyName = TargetForeignKeyPropertyName,
                Values = values
            }
            : null;
    }

    internal override void Apply(IReadOnlyList<AuditChange> changes, AuditEnrichmentContext context)
    {
        var loadedEntities = context.GetLoadedEntities(TargetEntityType, TargetForeignKeyPropertyName);
        var entitiesByForeignKey = LoadedEntityLookup.ManyByKey(loadedEntities, TargetForeignKeySelector);

        foreach (var change in changes)
        {
            var sourceKey = SourceKeySelector(change);
            if (sourceKey is null)
                continue;

            if (!entitiesByForeignKey.TryGetValue(sourceKey, out var related))
                continue;

            Map(change, related, context.GetBagForChange(change));
        }
    }
}