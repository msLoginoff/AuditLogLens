namespace AuditLog.Enrichment;

public sealed class AuditEntityEnricherRegistry
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
}