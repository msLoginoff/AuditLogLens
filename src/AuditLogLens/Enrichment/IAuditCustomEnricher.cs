namespace AuditLog.Enrichment;

public interface IAuditCustomEnricher
{
    bool CanHandle(Type entityType);

    void Configure(IAuditEnrichmentPlanBuilder builder, IReadOnlyList<AuditChange> changes);

    void Apply(AuditEnrichmentContext context);
}