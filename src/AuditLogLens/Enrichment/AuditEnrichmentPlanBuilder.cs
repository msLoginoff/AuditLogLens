namespace AuditLog.Enrichment;

public sealed class AuditEnrichmentPlanBuilder : IAuditEnrichmentPlanBuilder
{
    private readonly AuditEnrichmentPlan _plan = new();

    public void RequireEntityType(Type entityType)
    {
        _plan.RequireEntityType(entityType);
    }

    public AuditEnrichmentPlan Build()
    {
        return _plan;
    }
}