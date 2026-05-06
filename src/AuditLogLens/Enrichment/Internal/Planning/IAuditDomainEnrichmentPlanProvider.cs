namespace AuditLogLens.Enrichment.Internal.Planning;

internal interface IAuditDomainEnrichmentPlanProvider
{
    AuditEnrichmentPlan GetPlan(Type entityType);
}