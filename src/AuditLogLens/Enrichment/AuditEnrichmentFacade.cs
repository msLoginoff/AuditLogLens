using AuditLog.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace AuditLog.Enrichment;

public sealed class AuditEnrichmentFacade : IAuditEnricher
{
    private readonly IReadOnlyCollection<IAuditCustomEnricher> _customEnrichers;

    public AuditEnrichmentFacade(IEnumerable<IAuditCustomEnricher> customEnrichers)
    {
        _customEnrichers = customEnrichers.ToList();
    }

    public void Enrich(List<AuditChange> changes, DbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(changes);
        ArgumentNullException.ThrowIfNull(dbContext);

        if (changes.Count == 0)
        {
            return;
        }

        var context = new AuditEnrichmentContext(changes, dbContext);
        var entityTypes = changes.Select(x => x.EntityType).Distinct().ToList();

        foreach (var entityType in entityTypes)
        {
            var plan = BuildPlanForType(entityType, changes);
            ApplyPlanForType(plan, context, dbContext);
        }

        foreach (var enricher in _customEnrichers.Where(x => entityTypes.Any(x.CanHandle)))
        {
            enricher.Apply(context);
        }

        ApplyBagsToChanges(context);
    }

    private AuditEnrichmentPlan BuildPlanForType(
        Type entityType,
        IReadOnlyList<AuditChange> changes)
    {
        var builder = new AuditEnrichmentPlanBuilder();

        foreach (var enricher in _customEnrichers.Where(x => x.CanHandle(entityType)))
        {
            enricher.Configure(builder, changes);
        }

        return builder.Build();
    }

    private static void ApplyPlanForType(
        AuditEnrichmentPlan plan,
        AuditEnrichmentContext context,
        DbContext dbContext)
    {
        // TODO:
        // позже здесь будет батч-подгрузка сущностей по enrichment plan.
    }

    private static void ApplyBagsToChanges(AuditEnrichmentContext context)
    {
        foreach (var change in context.Changes)
        {
            var bag = context.GetBagForChange(change);

            foreach (var pair in bag.Values)
            {
                change.NewValues[pair.Key] = pair.Value;
            }
        }
    }
}