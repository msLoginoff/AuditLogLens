namespace AuditLogLens.Enrichment.Internal;

internal sealed class AuditEntityEnricherRegistry
{
    private readonly IReadOnlyList<IAuditEntityEnricher> _enrichers;

    public AuditEntityEnricherRegistry(IEnumerable<IAuditEntityEnricher> enrichers)
    {
        ArgumentNullException.ThrowIfNull(enrichers);
        _enrichers = enrichers.ToList();
    }

    public IReadOnlyList<IAuditEntityEnricher> GetEnrichersFor(Type entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);

        return _enrichers
            .Where(x => x.CanHandle(entityType))
            .ToList();
    }

    public IReadOnlyList<IAuditEntityEnricher> GetDistinctEnrichersFor(IReadOnlyList<Type> entityTypes)
    {
        ArgumentNullException.ThrowIfNull(entityTypes);

        var seen = new HashSet<IAuditEntityEnricher>(ReferenceEqualityComparer.Instance);
        var result = new List<IAuditEntityEnricher>();

        foreach (var entityType in entityTypes)
        foreach (var enricher in GetEnrichersFor(entityType))
            if (seen.Add(enricher))
                result.Add(enricher);

        return result;
    }
}