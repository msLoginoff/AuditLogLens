namespace AuditLogLens.Enrichment;

public interface IAuditEnrichmentPlanBuilder
{
    IAuditEnrichmentPlanBuilder AddRule(EnrichmentRule rule);

    IAuditEnrichmentPlanBuilder AddCustomStep(Action<AuditEnrichmentContext> step);
}