using AuditLogLens.Changes;
using AuditLogLens.Enrichment.Context;
using AuditLogLens.Enrichment.Internal.Loading;

namespace AuditLogLens.Enrichment.Rules;

/// <summary>
/// Loads entities that point back to the audited source and lets a mapper write
/// derived audit values.
/// </summary>
/// <remarks>
/// Use this rule when the audited entity does not store the values to display, but
/// related rows can be found by a foreign key that points to the audited entity.
/// </remarks>
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
        var loadedEntities = context.GetLoadedEntitiesOf(TargetEntityType, TargetForeignKeyPropertyName);
        var entitiesByForeignKey = LoadedEntityLookup.ManyByKey(loadedEntities, TargetForeignKeySelector);

        foreach (var change in changes)
        {
            var sourceKey = SourceKeySelector(change);
            if (sourceKey is null)
                continue;

            if (!entitiesByForeignKey.TryGetValue(sourceKey, out var related))
                continue;

            Map(change, related, context.GetBagFor(change));
        }
    }
}
