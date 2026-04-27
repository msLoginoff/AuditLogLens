namespace AuditLog.Enrichment;

public abstract class AuditEntityEnricherBase : IAuditEntityEnricher
{
    public abstract bool CanHandle(Type entityType);

    public virtual void Configure(IAuditEnrichmentPlanBuilder builder)
    {
    }

    public void Apply(AuditEnrichmentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        ApplyCustom(context);
        context.FlushBagsToChanges();
        AfterApply(context);
    }

    protected virtual void ApplyCustom(AuditEnrichmentContext context)
    {
    }

    protected virtual void AfterApply(AuditEnrichmentContext context)
    {
    }
}