using AuditLogLens.Enrichment.Planning;

namespace AuditLogLens.Enrichment.Domain;

public interface IAuditDomainEnrichmentPlanProvider
{
    AuditEnrichmentPlan GetPlan(Type entityType);
}