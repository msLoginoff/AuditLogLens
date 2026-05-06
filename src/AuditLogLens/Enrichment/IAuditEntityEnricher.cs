using AuditLogLens.Enrichment.Context;

namespace AuditLogLens.Enrichment;

public interface IAuditEntityEnricher
{
    bool CanHandle(Type entityType);

    void Configure(IAuditEnrichmentPlanBuilder builder);

    Task ApplyAsync(AuditEnrichmentContext context, CancellationToken cancellationToken = default);
}