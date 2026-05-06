using AuditLogLens.Enrichment.Context;
using AuditLogLens.Enrichment.Internal.Loading;
using AuditLogLens.Enrichment.Internal.Planning;
using Microsoft.EntityFrameworkCore;

namespace AuditLogLens.Enrichment.Internal;

internal sealed class AuditEnrichmentFacade : IAuditEnricher
{
    private readonly IAuditDomainEnrichmentPlanProvider _domainPlanProvider;
    private readonly AuditEntityEnricherRegistry _enricherRegistry;

    public AuditEnrichmentFacade(
        IAuditDomainEnrichmentPlanProvider domainPlanProvider,
        AuditEntityEnricherRegistry enricherRegistry)
    {
        _domainPlanProvider = domainPlanProvider;
        _enricherRegistry = enricherRegistry;
    }

    public async Task EnrichAsync(
        List<AuditChange> changes,
        DbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(changes);
        ArgumentNullException.ThrowIfNull(dbContext);

        if (changes.Count == 0)
            return;

        var context = new AuditEnrichmentContext(changes, dbContext);
        var entityTypes = context.EntityTypes;
        var plansByEntityType = context.EntityTypes.ToDictionary(
            entityType => entityType,
            BuildCombinedPlan);

        var loadRequests = CollectLoadRequests(plansByEntityType, context);
        await EnrichmentDataLoader.LoadAsync(loadRequests, context, cancellationToken)
            .ConfigureAwait(false);

        // Rules are applied only after all reference data has been globally preloaded
        foreach (var (entityType, plan) in plansByEntityType)
        {
            ApplyRules(entityType, plan, context);
        }

        // Entity enrichers run once per save operation, even if they handle several entity types
        foreach (var enricher in _enricherRegistry.GetDistinctEnrichersFor(entityTypes))
            await enricher.ApplyAsync(context, cancellationToken).ConfigureAwait(false);

        context.FlushBagsToChanges();
    }

    private AuditEnrichmentPlan BuildCombinedPlan(Type entityType)
    {
        var builder = new AuditEnrichmentPlanBuilder()
            .Merge(_domainPlanProvider.GetPlan(entityType));

        foreach (var enricher in _enricherRegistry.GetEnrichersFor(entityType))
            enricher.Configure(builder);

        return builder.Build();
    }

    private static IReadOnlyList<EntityLoadRequest> CollectLoadRequests(
        IReadOnlyDictionary<Type, AuditEnrichmentPlan> plansByEntityType,
        AuditEnrichmentContext context)
    {
        var requests = new List<EntityLoadRequest>();

        foreach (var (entityType, plan) in plansByEntityType)
        {
            var changes = context.GetChangesOfType(entityType);
            if (changes.Count == 0)
                continue;

            requests.AddRange(
                plan.Rules
                    .Select(rule => rule.BuildLoadRequest(changes))
                    .OfType<EntityLoadRequest>());
        }

        return requests;
    }

    private static void ApplyRules(
        Type entityType,
        AuditEnrichmentPlan plan,
        AuditEnrichmentContext context)
    {
        var changes = context.GetChangesOfType(entityType);
        if (changes.Count == 0)
            return;

        foreach (var rule in plan.Rules)
            rule.Apply(changes, context);

        foreach (var customStep in plan.CustomSteps)
            customStep(context);
    }
}