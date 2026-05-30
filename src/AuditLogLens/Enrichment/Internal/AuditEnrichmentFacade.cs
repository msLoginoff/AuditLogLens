using AuditLogLens.Changes;
using AuditLogLens.Detection.Internal;
using AuditLogLens.Enrichment.Context;
using AuditLogLens.Enrichment.Internal.Loading;
using AuditLogLens.Enrichment.Internal.Planning;
using Microsoft.EntityFrameworkCore;

namespace AuditLogLens.Enrichment.Internal;

internal sealed class AuditEnrichmentFacade : IAuditEnricher
{
    private readonly AuditEnrichmentPlanResolver _planResolver;
    private readonly AuditEntityEnricherRegistry _enricherRegistry;

    public AuditEnrichmentFacade(
        AuditEnrichmentPlanResolver planResolver,
        AuditEntityEnricherRegistry enricherRegistry)
    {
        _planResolver = planResolver;
        _enricherRegistry = enricherRegistry;
    }

    public async Task EnrichAsync(
        List<AuditChange> changes,
        DbContext dbContext,
        IReadOnlyList<AuditTrackedEntry> trackedEntries,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(changes);
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(trackedEntries);

        if (changes.Count == 0)
            return;

        var context = new AuditEnrichmentContext(changes, dbContext, trackedEntries);
        var entityTypes = context.EntityTypes;
        var plansByEntityType = context.EntityTypes.ToDictionary(
            entityType => entityType,
            _planResolver.ResolvePlanFor);

        var loadRequests = CollectLoadRequests(plansByEntityType, context);
        await EntityLoadRequestExecutor.ExecuteAsync(loadRequests, context, cancellationToken)
            .ConfigureAwait(false);

        // Rules are applied only after all reference data has been globally preloaded
        foreach (var (entityType, plan) in plansByEntityType)
        {
            ApplyRules(entityType, plan, context);
        }

        var enrichers = _enricherRegistry.GetDistinctEnrichersFor(entityTypes);

        // Entity enrichers run once per save operation, even if they handle several entity types
        foreach (var enricher in enrichers)
            await enricher.ApplyBeforeMergeAsync(context, cancellationToken).ConfigureAwait(false);

        context.MergeBagsToChanges();

        foreach (var enricher in enrichers)
            await enricher.ApplyAfterMergeAsync(context, cancellationToken).ConfigureAwait(false);
    }

    private static IReadOnlyList<EntityLoadRequest> CollectLoadRequests(
        IReadOnlyDictionary<Type, AuditEnrichmentPlan> plansByEntityType,
        AuditEnrichmentContext context)
    {
        var requests = new List<EntityLoadRequest>();

        foreach (var (entityType, plan) in plansByEntityType)
        {
            var changes = context.GetChangesOf(entityType);
            if (changes.Count == 0)
                continue;

            requests.AddRange(
                plan.Rules
                    .Select(rule => rule.BuildLoadRequest(changes, context))
                    .OfType<EntityLoadRequest>());
        }

        return requests;
    }

    private static void ApplyRules(
        Type entityType,
        AuditEnrichmentPlan plan,
        AuditEnrichmentContext context)
    {
        var changes = context.GetChangesOf(entityType);
        if (changes.Count == 0)
            return;

        foreach (var rule in plan.Rules)
            rule.Apply(changes, context);

        foreach (var customStep in plan.CustomSteps)
            customStep(context);
    }
}