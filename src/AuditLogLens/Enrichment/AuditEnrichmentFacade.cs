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
            .GetMethod(nameof(LoadEntitiesGeneric), BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException(
            $"Method {nameof(LoadEntitiesGeneric)} was not found.");

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

    public void Enrich(List<AuditChange> changes, DbContext dbContext)
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
            ApplyRules(entityType, plan, context);
        }

        // Phase 2: each enricher called exactly once, after all data is loaded
        foreach (var enricher in _enricherRegistry.GetDistinctEnrichersFor(entityTypes))
            enricher.Apply(context);

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

    private void ApplyRules(Type entityType, AuditEnrichmentPlan plan, AuditEnrichmentContext context)
    {
        var changes = context.GetChangesOfType(entityType).ToList();
        if (changes.Count == 0)
            return;

        LoadRequiredEntities(plan.Rules, changes, context);

        foreach (var rule in plan.Rules)
            rule.Apply(changes, context);

        foreach (var customStep in plan.CustomSteps)
            customStep(context);
    }

    private static void LoadRequiredEntities(
        IReadOnlyCollection<EnrichmentRule> rules,
        IReadOnlyList<AuditChange> changes,
        AuditEnrichmentContext context)
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

            var loaded = LoadEntitiesByPropertyValues(
                context.DbContext, entityType, group.Key.PropertyName, values);

            context.SetLoadedEntities(entityType, Deduplicate(context.DbContext, entityType, loaded));
        }
    }

    private static IReadOnlyList<object> LoadEntitiesByPropertyValues(
        DbContext dbContext,
        Type entityType,
        string propertyName,
        IReadOnlyList<object> values)
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

        return (IReadOnlyList<object>)(method.Invoke(null, [dbContext, propertyName, values])
                                       ?? Array.Empty<object>());
    }

    private static IReadOnlyList<object> LoadEntitiesGeneric<TEntity, TProperty>(
        DbContext dbContext,
        string propertyName,
        IReadOnlyList<object> rawValues)
        where TEntity : class
    {
        var typedValues = rawValues.Select(x => (TProperty)x).Distinct().ToList();

        if (typedValues.Count == 0)
            return Array.Empty<object>();

        return dbContext
            .Set<TEntity>()
            .AsNoTracking()
            .Where(x => typedValues.Contains(EF.Property<TProperty>(x, propertyName)))
            .Cast<object>()
            .ToList();
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