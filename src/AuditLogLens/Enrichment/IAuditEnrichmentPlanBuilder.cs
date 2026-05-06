using AuditLogLens.Enrichment.Context;
using AuditLogLens.Enrichment.Rules;

namespace AuditLogLens.Enrichment;

public interface IAuditEnrichmentPlanBuilder
{
    IAuditEnrichmentPlanBuilder AddRule(EnrichmentRule rule);

    IAuditEnrichmentPlanBuilder AddCustomStep(Action<AuditEnrichmentContext> step);
}