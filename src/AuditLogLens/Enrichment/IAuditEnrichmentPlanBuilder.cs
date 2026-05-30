using AuditLogLens.Enrichment.Context;
using AuditLogLens.Enrichment.Rules;

namespace AuditLogLens.Enrichment;

/// <summary>
/// Builds the enrichment plan for one audited type.
/// </summary>
public interface IAuditEnrichmentPlanBuilder
{
    /// <summary>
    /// Adds an enrichment rule to the plan.
    /// </summary>
    IAuditEnrichmentPlanBuilder AddRule(EnrichmentRule rule);

    /// <summary>
    /// Adds a custom step that runs after rule-based enrichment.
    /// </summary>
    IAuditEnrichmentPlanBuilder AddCustomStep(Action<AuditEnrichmentContext> step);
}
