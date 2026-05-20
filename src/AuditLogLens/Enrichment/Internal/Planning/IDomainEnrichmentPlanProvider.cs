namespace AuditLogLens.Enrichment.Internal.Planning;

internal interface IDomainEnrichmentPlanProvider
{
    AuditEnrichmentPlan GetPlanFor(Type entityType);
}