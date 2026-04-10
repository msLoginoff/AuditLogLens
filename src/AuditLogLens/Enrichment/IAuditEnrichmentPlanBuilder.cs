namespace AuditLog.Enrichment;

public interface IAuditEnrichmentPlanBuilder
{
    void RequireEntityType(Type entityType);

    void AddRule(EnrichmentRule rule);

    void AddCustomStep(Action<AuditEnrichmentContext> step);

    AuditEnrichmentPlan Build();
}