using AuditLogLens.Changes;
using AuditLogLens.Enrichment.Context;

namespace AuditLogLens.Enrichment;

/// <summary>
/// Base class for application-level audit enrichment.
/// </summary>
/// <remarks>
/// Override <see cref="Configure"/> to declare batched lookup needs. Use before-merge hooks
/// to write staged values to an <see cref="AuditEnrichmentBag"/>. Use after-merge hooks when
/// the final values are already on <see cref="AuditChange"/>.
/// </remarks>
public abstract class AuditEntityEnricherBase : IAuditEntityEnricher
{
    /// <summary>
    /// Determines whether this enricher should run for changes with the specified entity type.
    /// </summary>
    public abstract bool CanHandle(Type entityType);

    /// <summary>
    /// Declares enrichment rules and lookup requirements for this enricher.
    /// </summary>
    public virtual void Configure(IAuditEnrichmentPlanBuilder builder)
    {
    }

    /// <summary>
    /// Runs the before-merge phase for this enricher.
    /// </summary>
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
                    context.GetBagFor(change),
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Runs the after-merge phase for this enricher.
    /// </summary>
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

    /// <summary>
    /// Runs once before per-change before-merge hooks.
    /// </summary>
    protected virtual Task BeforeMergeAsync(
        AuditEnrichmentContext context,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>
    /// Runs for each handled change before staged values are merged into the change.
    /// </summary>
    protected virtual Task BeforeMergeChangeAsync(
        AuditEnrichmentContext context,
        AuditChange change,
        AuditEnrichmentBag bag,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>
    /// Runs for each handled change after staged values have been merged into the change.
    /// </summary>
    protected virtual Task AfterMergeChangeAsync(
        AuditEnrichmentContext context,
        AuditChange change,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>
    /// Runs once after all per-change after-merge hooks.
    /// </summary>
    protected virtual Task AfterMergeAsync(
        AuditEnrichmentContext context,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    private IEnumerable<AuditChange> GetHandledChanges(AuditEnrichmentContext context)
    {
        return context.Changes.Where(change => CanHandle(change.EntityType));
    }
}
