using AuditLogLens.Enrichment.Planning;

namespace AuditLogLens.Enrichment.EntityEnrichers;

public interface IAuditEntityEnricher
{
    bool CanHandle(Type entityType);

    void Configure(IAuditEnrichmentPlanBuilder builder);

    Task ApplyAsync(AuditEnrichmentContext context, CancellationToken cancellationToken = default);
}