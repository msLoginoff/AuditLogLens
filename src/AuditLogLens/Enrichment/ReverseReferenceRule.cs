namespace AuditLog.Enrichment;

public sealed class ReverseReferenceRule : EnrichmentRule
{
    public required Type TargetEntityType { get; init; }

    public required Func<AuditChange, object?> SourceKeySelector { get; init; }

    public required string TargetForeignKeyPropertyName { get; init; }

    public required Func<object, object?> TargetForeignKeySelector { get; init; }

    public required Action<AuditChange, IReadOnlyList<object>, AuditEnrichmentBag> Map { get; init; }

    internal override EntityLoadRequest? BuildLoadRequest(IReadOnlyList<AuditChange> changes)
    {
        var values = changes
            .Select(SourceKeySelector)
            .Where(x => x is not null)
            .Cast<object>()
            .Distinct()
            .ToList();

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

        foreach (var change in changes)
        {
            var sourceKey = SourceKeySelector(change);
            if (sourceKey is null)
                continue;

            var related = loadedEntities
                .Where(x => Equals(TargetForeignKeySelector(x), sourceKey))
                .ToList();

            if (related.Count == 0)
                continue;

            Map(change, related, context.GetBagForChange(change));
        }
    }
}