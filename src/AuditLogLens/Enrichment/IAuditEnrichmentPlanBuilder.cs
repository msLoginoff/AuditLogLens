namespace AuditLog.Enrichment;

public interface IAuditEnrichmentPlanBuilder
{
    void RequireEntityType(Type entityType);

    AuditEnrichmentPlan Build();
}