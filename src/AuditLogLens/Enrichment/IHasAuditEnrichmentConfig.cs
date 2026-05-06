namespace AuditLogLens.Enrichment;

public interface IHasAuditEnrichmentConfig<TSelf>
    where TSelf : IHasAuditEnrichmentConfig<TSelf>
{
    static abstract void ConfigureAuditEnrichment(IAuditEnrichmentPlanBuilder builder);
}