using AuditLogLens.Enrichment.Loading;

namespace AuditLogLens.Enrichment;

public abstract class EnrichmentRule
{
    public string? Description { get; init; }

    internal abstract EntityLoadRequest? BuildLoadRequest(IReadOnlyList<AuditChange> changes);

    internal abstract void Apply(IReadOnlyList<AuditChange> changes, AuditEnrichmentContext context);
}