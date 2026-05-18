using AuditLogLens;
using AuditLogLens.Enrichment.Context;

namespace AuditLogLens.Enrichment;

public abstract class AuditEntityEnricherBase : IAuditEntityEnricher
{
    public abstract bool CanHandle(Type entityType);

    public virtual void Configure(IAuditEnrichmentPlanBuilder builder)
    {
    }

    public async Task ApplyBeforeMergeAsync(
        AuditEnrichmentContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        await BeforeMergeAsync(context, cancellationToken).ConfigureAwait(false);
        foreach (var change in GetHandledChanges(context))
        {
            await BeforeMergeChangeAsync(
                    context,
                    change,
                    context.GetBagForChange(change),
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    public async Task ApplyAfterMergeAsync(
        AuditEnrichmentContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var change in GetHandledChanges(context))
        {
            await AfterMergeChangeAsync(context, change, cancellationToken).ConfigureAwait(false);
        }

        await AfterMergeAsync(context, cancellationToken).ConfigureAwait(false);
    }

    protected virtual Task BeforeMergeAsync(
        AuditEnrichmentContext context,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    protected virtual Task BeforeMergeChangeAsync(
        AuditEnrichmentContext context,
        AuditChange change,
        AuditEnrichmentBag bag,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    protected virtual Task AfterMergeChangeAsync(
        AuditEnrichmentContext context,
        AuditChange change,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    protected virtual Task AfterMergeAsync(
        AuditEnrichmentContext context,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    private IEnumerable<AuditChange> GetHandledChanges(AuditEnrichmentContext context)
    {
        return context.Changes.Where(change => CanHandle(change.EntityType));
    }
}