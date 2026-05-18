using AuditLogLens.Enrichment.Context;

namespace AuditLogLens.Enrichment;

public interface IAuditEntityEnricher
{
    bool CanHandle(Type entityType);

    void Configure(IAuditEnrichmentPlanBuilder builder);

    Task ApplyBeforeMergeAsync(AuditEnrichmentContext context, CancellationToken cancellationToken = default);

    Task ApplyAfterMergeAsync(AuditEnrichmentContext context, CancellationToken cancellationToken = default);
}