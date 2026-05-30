namespace AuditLogLens.Enrichment;

/// <summary>
/// Defines domain-level enrichment rules for an audited type.
/// </summary>
/// <typeparam name="TSelf">The audited type that owns the configuration.</typeparam>
public interface IHasAuditEnrichmentConfig<TSelf>
    where TSelf : IHasAuditEnrichmentConfig<TSelf>
{
    /// <summary>
    /// Adds enrichment rules for the audited type.
    /// </summary>
    static abstract void ConfigureAuditEnrichment(IAuditEnrichmentPlanBuilder builder);
}
