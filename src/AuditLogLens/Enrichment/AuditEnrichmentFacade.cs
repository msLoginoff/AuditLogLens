using System.Collections.Concurrent;
using System.Reflection;
using AuditLogLens.Abstractions;
using AuditLogLens.Enrichment.Domain;
using Microsoft.EntityFrameworkCore;

namespace AuditLogLens.Enrichment;

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
        var plansByEntityType = entityTypes.ToDictionary(
            entityType => entityType,
            BuildCombinedPlan);

        var loadRequests = CollectLoadRequests(plansByEntityType, context);
        await LoadRequiredEntitiesAsync(loadRequests, context, cancellationToken).ConfigureAwait(false);

        // Phase 1: apply rules after all required data has been globally preloaded
        foreach (var (entityType, plan) in plansByEntityType)
        {
            ApplyRules(entityType, plan, context);
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

    private static IReadOnlyList<EntityLoadRequest> CollectLoadRequests(
        IReadOnlyDictionary<Type, AuditEnrichmentPlan> plansByEntityType,
        AuditEnrichmentContext context)
    {
        var requests = new List<EntityLoadRequest>();

        foreach (var (entityType, plan) in plansByEntityType)
        {
            var changes = context.GetChangesOfType(entityType).ToList();
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
        var changes = context.GetChangesOfType(entityType).ToList();
        if (changes.Count == 0)
            return;

        foreach (var rule in plan.Rules)
            rule.Apply(changes, context);

        foreach (var customStep in plan.CustomSteps)
            customStep(context);
    }

    private static async Task LoadRequiredEntitiesAsync(
        IReadOnlyCollection<EntityLoadRequest> loadRequests,
        AuditEnrichmentContext context,
        CancellationToken cancellationToken)
    {
        var groups = loadRequests.GroupBy(r => (r.EntityType, r.PropertyName));

        foreach (var group in groups)
        {
            var entityType = group.Key.EntityType;
            var propertyName = group.Key.PropertyName;

            var values = group.SelectMany(r => r.Values).Distinct().ToList();
            if (values.Count == 0)
                continue;

            var loaded = await LoadEntitiesByPropertyValuesAsync(
                    context.DbContext, entityType, propertyName, values, cancellationToken)
                .ConfigureAwait(false);

            context.SetLoadedEntities(
                entityType,
                propertyName,
                Deduplicate(context.DbContext, entityType, loaded));
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