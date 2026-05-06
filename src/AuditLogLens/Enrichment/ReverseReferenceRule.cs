namespace AuditLogLens.Enrichment;

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
        var entitiesByForeignKey = BuildEntityLookup(loadedEntities);

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

    private Dictionary<object, List<object>> BuildEntityLookup(IReadOnlyList<object> loadedEntities)
    {
        var result = new Dictionary<object, List<object>>();

        foreach (var entity in loadedEntities)
        {
            var foreignKey = TargetForeignKeySelector(entity);
            if (foreignKey is null)
                continue;

            if (!result.TryGetValue(foreignKey, out var related))
            {
                related = [];
                result[foreignKey] = related;
            }

            related.Add(entity);
        }

        return result;
    }
}