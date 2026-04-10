namespace AuditLog.Enrichment.Domain;

public interface IAuditDomainEnrichmentPlanProvider
{
    AuditEnrichmentPlan GetPlan(Type entityType);
}