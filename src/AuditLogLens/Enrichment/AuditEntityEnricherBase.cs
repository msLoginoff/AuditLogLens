using AuditLogLens.Enrichment.Context;

namespace AuditLogLens.Enrichment;

public abstract class AuditEntityEnricherBase : IAuditEntityEnricher
{
    public abstract bool CanHandle(Type entityType);

    public virtual void Configure(IAuditEnrichmentPlanBuilder builder)
    {
    }

    public async Task ApplyAsync(AuditEnrichmentContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        await ApplyCustomAsync(context, cancellationToken).ConfigureAwait(false);
        context.MergeBagsToChanges();
        await AfterApplyAsync(context, cancellationToken).ConfigureAwait(false);
    }

    protected virtual Task ApplyCustomAsync(AuditEnrichmentContext context,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    protected virtual Task AfterApplyAsync(AuditEnrichmentContext context,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}