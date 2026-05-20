using AuditLogLens.Enrichment.Rules;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace AuditLogLens.Enrichment.Internal.Planning;

internal sealed class AuditEnrichmentPlanResolver
{
    private readonly IDomainEnrichmentPlanProvider _domainPlanProvider;
    private readonly AuditEntityEnricherRegistry _enricherRegistry;
    private readonly Dictionary<Type, AuditEnrichmentPlan> _planCacheByEntityType = new();
    private readonly Dictionary<IModel, IReadOnlyList<CollectionRule>> _collectionRulesCacheByEfModel = new();

    public AuditEnrichmentPlanResolver(
        IDomainEnrichmentPlanProvider domainPlanProvider,
        AuditEntityEnricherRegistry enricherRegistry)
    {
        _domainPlanProvider = domainPlanProvider;
        _enricherRegistry = enricherRegistry;
    }

    public AuditEnrichmentPlan ResolvePlanFor(Type entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);

        if (_planCacheByEntityType.TryGetValue(entityType, out var plan))
        {
            return plan;
        }

        var builder = new AuditEnrichmentPlanBuilder()
            .Merge(_domainPlanProvider.GetPlanFor(entityType));

        foreach (var enricher in _enricherRegistry.GetEnrichersFor(entityType))
        {
            enricher.Configure(builder);
        }

        plan = builder.Build();
        _planCacheByEntityType[entityType] = plan;

        return plan;
    }

    public IReadOnlyList<CollectionRule> ResolveCollectionRules(DbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        if (_collectionRulesCacheByEfModel.TryGetValue(dbContext.Model, out var rules))
        {
            return rules;
        }

        rules = dbContext.Model
            .GetEntityTypes()
            .Select(entityType => entityType.ClrType)
            .Distinct()
            .Select(ResolvePlanFor)
            .SelectMany(plan => plan.Rules)
            .OfType<CollectionRule>()
            .ToList();

        _collectionRulesCacheByEfModel[dbContext.Model] = rules;

        return rules;
    }
}