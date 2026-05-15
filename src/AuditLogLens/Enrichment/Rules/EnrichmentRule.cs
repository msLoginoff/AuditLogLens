using AuditLogLens.Enrichment.Context;
using AuditLogLens.Enrichment.Internal.Loading;

namespace AuditLogLens.Enrichment.Rules;

public abstract class EnrichmentRule
{
    public string? Description { get; init; }

    internal abstract EntityLoadRequest? BuildLoadRequest(
        IReadOnlyList<AuditChange> changes,
        AuditEnrichmentContext context);

    internal abstract void Apply(IReadOnlyList<AuditChange> changes, AuditEnrichmentContext context);
}