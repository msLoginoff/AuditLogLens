namespace AuditLog.Enrichment.Domain;

public interface IHasAuditEnrichmentConfig<TSelf>
    where TSelf : IHasAuditEnrichmentConfig<TSelf>
{
    static abstract void ConfigureAuditEnrichment(IAuditEnrichmentPlanBuilder builder);
}