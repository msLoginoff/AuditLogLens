using System.Collections.Concurrent;
using System.Reflection;
using AuditLog.Abstractions;
using AuditLog.Enrichment.Domain;
using Microsoft.EntityFrameworkCore;

namespace AuditLog.Enrichment;

public sealed class AuditEnrichmentFacade : IAuditEnricher
{
    private static readonly MethodInfo LoadEntitiesGenericMethod =
        typeof(AuditEnrichmentFacade)
            .GetMethod(nameof(LoadEntitiesGenericAsync), BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException(
            $"Method {nameof(LoadEntitiesGenericAsync)} was not found.");

    private static readonly ConcurrentDictionary<(Type Entity, Type Key), MethodInfo> ClosedMethodCache = new();

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
        var entityTypes = changes.Select(x => x.EntityType).Distinct().ToList();

        // Phase 1: load data + apply rules per entity type
        foreach (var entityType in entityTypes)
        {
            var plan = BuildCombinedPlan(entityType);
            await ApplyRulesAsync(entityType, plan, context, cancellationToken).ConfigureAwait(false);
        }

        // Phase 2: each enricher called exactly once, after all data is loaded
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

    private async Task ApplyRulesAsync(
        Type entityType,
        AuditEnrichmentPlan plan,
        AuditEnrichmentContext context,
        CancellationToken cancellationToken)
    {
        var changes = context.GetChangesOfType(entityType).ToList();
        if (changes.Count == 0)
            return;

        await LoadRequiredEntitiesAsync(plan.Rules, changes, context, cancellationToken).ConfigureAwait(false);

        foreach (var rule in plan.Rules)
            rule.Apply(changes, context);

        foreach (var customStep in plan.CustomSteps)
            customStep(context);
    }

    private static async Task LoadRequiredEntitiesAsync(
        IReadOnlyCollection<EnrichmentRule> rules,
        IReadOnlyList<AuditChange> changes,
        AuditEnrichmentContext context,
        CancellationToken cancellationToken)
    {
        var groups = rules
            .Select(r => r.BuildLoadRequest(changes))
            .OfType<EntityLoadRequest>()
            .GroupBy(r => (r.EntityType, r.PropertyName));

        foreach (var group in groups)
        {
            var entityType = group.Key.EntityType;

            if (context.GetLoadedEntities(entityType).Count > 0)
                continue;

            var values = group.SelectMany(r => r.Values).Distinct().ToList();
            if (values.Count == 0)
                continue;

            var loaded = await LoadEntitiesByPropertyValuesAsync(
                    context.DbContext, entityType, group.Key.PropertyName, values, cancellationToken)
                .ConfigureAwait(false);

            context.SetLoadedEntities(entityType, Deduplicate(context.DbContext, entityType, loaded));
        }
    }

    private static Task<IReadOnlyList<object>> LoadEntitiesByPropertyValuesAsync(
        DbContext dbContext,
        Type entityType,
        string propertyName,
        IReadOnlyList<object> values,
        CancellationToken cancellationToken)
    {
        var efEntityType = dbContext.Model.FindEntityType(entityType)
                           ?? throw new InvalidOperationException(
                               $"Entity type {entityType.FullName} is not part of the current DbContext model.");

        var property = efEntityType.FindProperty(propertyName)
                       ?? throw new InvalidOperationException(
                           $"Property '{propertyName}' was not found on entity type {entityType.FullName}.");

        var method = ClosedMethodCache.GetOrAdd(
            (entityType, property.ClrType),
            static key => LoadEntitiesGenericMethod.MakeGenericMethod(key.Entity, key.Key));

        return (Task<IReadOnlyList<object>>)(method.Invoke(null, [dbContext, propertyName, values, cancellationToken])
                                             ?? Task.FromResult<IReadOnlyList<object>>(Array.Empty<object>()));
    }

    private static async Task<IReadOnlyList<object>> LoadEntitiesGenericAsync<TEntity, TProperty>(
        DbContext dbContext,
        string propertyName,
        IReadOnlyList<object> rawValues,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        var typedValues = rawValues.Select(x => (TProperty)x).Distinct().ToList();

        if (typedValues.Count == 0)
            return Array.Empty<object>();

        return await dbContext
            .Set<TEntity>()
            .AsNoTracking()
            .Where(x => typedValues.Contains(EF.Property<TProperty>(x, propertyName)))
            .Cast<object>()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static IReadOnlyList<object> Deduplicate(
        DbContext dbContext,
        Type entityType,
        IReadOnlyList<object> entities)
    {
        if (entities.Count <= 1)
            return entities;

        var efEntityType = dbContext.Model.FindEntityType(entityType)
                           ?? throw new InvalidOperationException(
                               $"Entity type {entityType.FullName} is not part of the current DbContext model.");

        var primaryKey = efEntityType.FindPrimaryKey();
        if (primaryKey is null || primaryKey.Properties.Count == 0)
            return entities;

        var seen = new HashSet<string>();
        var result = new List<object>();

        foreach (var entity in entities)
        {
            var key = string.Join("|", primaryKey.Properties.Select(p =>
                p.PropertyInfo?.GetValue(entity)?.ToString() ?? "<null>"));

            if (seen.Add(key))
                result.Add(entity);
        }

        return result;
    }
}