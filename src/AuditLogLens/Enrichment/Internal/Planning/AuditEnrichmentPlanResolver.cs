using AuditLogLens.Enrichment.Rules;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace AuditLogLens.Enrichment.Internal.Planning;

internal sealed class AuditEnrichmentPlanResolver
{
    private readonly IAuditDomainEnrichmentPlanProvider _domainPlanProvider;
    private readonly AuditEntityEnricherRegistry _enricherRegistry;
    private readonly Dictionary<Type, AuditEnrichmentPlan> _plansByEntityType = new();
    private readonly Dictionary<IModel, IReadOnlyList<CollectionRule>> _collectionRulesByModel = new();

    public AuditEnrichmentPlanResolver(
        IAuditDomainEnrichmentPlanProvider domainPlanProvider,
        AuditEntityEnricherRegistry enricherRegistry)
    {
        _domainPlanProvider = domainPlanProvider;
        _enricherRegistry = enricherRegistry;
    }

    public AuditEnrichmentPlan GetPlan(Type entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);

        if (_plansByEntityType.TryGetValue(entityType, out var plan))
        {
            return plan;
        }

        var builder = new AuditEnrichmentPlanBuilder()
            .Merge(_domainPlanProvider.GetPlan(entityType));

        foreach (var enricher in _enricherRegistry.GetEnrichersFor(entityType))
        {
            enricher.Configure(builder);
        }

        plan = builder.Build();
        _plansByEntityType[entityType] = plan;

        return plan;
    }

    public IReadOnlyList<CollectionRule> GetCollectionRules(DbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        if (_collectionRulesByModel.TryGetValue(dbContext.Model, out var rules))
        {
            return rules;
        }

        rules = dbContext.Model
            .GetEntityTypes()
            .Select(entityType => entityType.ClrType)
            .Distinct()
            .Select(GetPlan)
            .SelectMany(plan => plan.Rules)
            .OfType<CollectionRule>()
            .ToList();

        _collectionRulesByModel[dbContext.Model] = rules;

        return rules;
    }
}