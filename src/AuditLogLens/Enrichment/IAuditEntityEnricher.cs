namespace AuditLog.Enrichment;

public interface IAuditEntityEnricher
{
    bool CanHandle(Type entityType);

    void Configure(IAuditEnrichmentPlanBuilder builder);

    void Apply(AuditEnrichmentContext context);
}