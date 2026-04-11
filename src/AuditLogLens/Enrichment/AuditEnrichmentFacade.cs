using System.Reflection;
using AuditLog.Abstractions;
using AuditLog.Enrichment.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace AuditLog.Enrichment;

public sealed class AuditEnrichmentFacade : IAuditEnricher
{
    private static readonly MethodInfo LoadEntitiesGenericMethod =
        typeof(AuditEnrichmentFacade)
            .GetMethod(nameof(LoadEntitiesGeneric), BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException(
            $"Method {nameof(LoadEntitiesGeneric)} was not found.");

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
        {
            return;
        }

        var context = new AuditEnrichmentContext(changes, dbContext);
        var entityTypes = changes
            .Select(x => x.EntityType)
            .Distinct()
            .ToList();

        foreach (var entityType in entityTypes)
        {
            var plan = BuildCombinedPlan(entityType);
            ApplyPlan(entityType, plan, context);
        }

        ApplyBagsToChanges(context);
    }

    private AuditEnrichmentPlan BuildCombinedPlan(Type entityType)
    {
        var builder = new AuditEnrichmentPlanBuilder();

        var domainPlan = _domainPlanProvider.GetPlan(entityType);
        MergePlanIntoBuilder(domainPlan, builder);

        var enrichers = _enricherRegistry.GetEnrichersFor(entityType);
        foreach (var enricher in enrichers)
        {
            enricher.Configure(builder);
        }

        return builder.Build();
    }

    private void ApplyPlan(
        Type entityType,
        AuditEnrichmentPlan plan,
        AuditEnrichmentContext context)
    {
        var changes = context.GetChangesOfType(entityType).ToList();
        if (changes.Count == 0)
        {
            return;
        }

        LoadRequiredEntities(plan, context, changes);

        foreach (var rule in plan.Rules)
        {
            ApplyRule(rule, changes, context);
        }

        foreach (var customStep in plan.CustomSteps)
        {
            customStep(context);
        }

        var enrichers = _enricherRegistry.GetEnrichersFor(entityType);
        foreach (var enricher in enrichers)
        {
            enricher.Apply(context);
        }
    }

    private static void MergePlanIntoBuilder(
        AuditEnrichmentPlan plan,
        IAuditEnrichmentPlanBuilder builder)
    {
        foreach (var entityType in plan.RequiredEntityTypes)
        {
            builder.RequireEntityType(entityType);
        }

        foreach (var rule in plan.Rules)
        {
            builder.AddRule(rule);
        }

        foreach (var step in plan.CustomSteps)
        {
            builder.AddCustomStep(step);
        }
    }

    private static void LoadRequiredEntities(
        AuditEnrichmentPlan plan,
        AuditEnrichmentContext context,
        IReadOnlyList<AuditChange> changes)
    {
        var loadRequests = BuildLoadRequests(plan, changes);

        foreach (var group in loadRequests.GroupBy(x => new { x.EntityType, x.PropertyName }))
        {
            var entityType = group.Key.EntityType;

            if (context.GetLoadedEntities(entityType).Count > 0)
            {
                continue;
            }

            var mergedValues = group
                .SelectMany(x => x.Values)
                .Distinct()
                .ToList();

            if (mergedValues.Count == 0)
            {
                continue;
            }

            var loadedEntities = LoadEntitiesByPropertyValues(
                context.DbContext,
                entityType,
                group.Key.PropertyName,
                mergedValues);

            context.SetLoadedEntities(
                entityType,
                DeduplicateEntities(context.DbContext, entityType, loadedEntities));
        }
    }

    private static IReadOnlyList<EntityLoadRequest> BuildLoadRequests(
        AuditEnrichmentPlan plan,
        IReadOnlyList<AuditChange> changes)
    {
        var requests = new List<EntityLoadRequest>();

        foreach (var rule in plan.Rules)
        {
            switch (rule)
            {
                case ReferenceRule referenceRule:
                {
                    var values = changes
                        .SelectMany(change =>
                        {
                            var (oldFk, newFk) = GetReferenceForeignKeys(change, referenceRule.ForeignKeyPropertyName);
                            return new[] { oldFk, newFk };
                        })
                        .Where(x => x is not null)
                        .Cast<object>()
                        .Distinct()
                        .ToList();

                    if (values.Count > 0)
                    {
                        requests.Add(new EntityLoadRequest
                        {
                            EntityType = referenceRule.TargetEntityType,
                            PropertyName = referenceRule.TargetKeyPropertyName,
                            Values = values
                        });
                    }

                    break;
                }

                case ReverseReferenceRule reverseReferenceRule:
                {
                    var values = changes
                        .Select(reverseReferenceRule.SourceKeySelector)
                        .Where(x => x is not null)
                        .Cast<object>()
                        .Distinct()
                        .ToList();

                    if (values.Count > 0)
                    {
                        requests.Add(new EntityLoadRequest
                        {
                            EntityType = reverseReferenceRule.TargetEntityType,
                            PropertyName = reverseReferenceRule.TargetForeignKeyPropertyName,
                            Values = values
                        });
                    }

                    break;
                }
            }
        }

        return requests;
    }

    private static IReadOnlyList<object> LoadEntitiesByPropertyValues(
        DbContext dbContext,
        Type entityType,
        string propertyName,
        IReadOnlyList<object> rawValues)
    {
        if (rawValues.Count == 0)
        {
            return Array.Empty<object>();
        }

        var efEntityType = dbContext.Model.FindEntityType(entityType)
                           ?? throw new InvalidOperationException(
                               $"Entity type {entityType.FullName} is not part of the current DbContext model.");

        var property = efEntityType.FindProperty(propertyName)
                       ?? throw new InvalidOperationException(
                           $"Property '{propertyName}' was not found on entity type {entityType.FullName}.");

        var propertyClrType = property.ClrType;

        var closedMethod = LoadEntitiesGenericMethod.MakeGenericMethod(entityType, propertyClrType);

        return (IReadOnlyList<object>)(closedMethod.Invoke(
                                           null,
                                           new object[] { dbContext, propertyName, rawValues })!
                                       ?? Array.Empty<object>());
    }

    private static IReadOnlyList<object> LoadEntitiesGeneric<TEntity, TProperty>(
        DbContext dbContext,
        string propertyName,
        IReadOnlyList<object> rawValues)
        where TEntity : class
    {
        var typedValues = rawValues
            .Select(x => (TProperty)x)
            .Distinct()
            .ToList();

        if (typedValues.Count == 0)
        {
            return Array.Empty<object>();
        }

        return dbContext
            .Set<TEntity>()
            .AsNoTracking()
            .Where(x => typedValues.Contains(EF.Property<TProperty>(x, propertyName)))
            .Cast<object>()
            .ToList();
    }

    private static IReadOnlyList<object> DeduplicateEntities(
        DbContext dbContext,
        Type entityType,
        IReadOnlyList<object> entities)
    {
        if (entities.Count <= 1)
        {
            return entities;
        }

        var efEntityType = dbContext.Model.FindEntityType(entityType)
                           ?? throw new InvalidOperationException(
                               $"Entity type {entityType.FullName} is not part of the current DbContext model.");

        var primaryKey = efEntityType.FindPrimaryKey();
        if (primaryKey is null || primaryKey.Properties.Count == 0)
        {
            return entities;
        }

        var result = new List<object>();
        var seen = new HashSet<string>();

        foreach (var entity in entities)
        {
            var key = BuildEntityKey(primaryKey, entity);
            if (seen.Add(key))
            {
                result.Add(entity);
            }
        }

        return result;
    }

    private static string BuildEntityKey(IKey primaryKey, object entity)
    {
        return string.Join(
            "|",
            primaryKey.Properties.Select(x =>
            {
                var value = x.PropertyInfo?.GetValue(entity);
                return value?.ToString() ?? "<null>";
            }));
    }

    private static void ApplyRule(
        EnrichmentRule rule,
        IReadOnlyList<AuditChange> changes,
        AuditEnrichmentContext context)
    {
        switch (rule)
        {
            case ReferenceRule referenceRule:
                ApplyReferenceRule(referenceRule, changes, context);
                break;

            case ReverseReferenceRule reverseReferenceRule:
                ApplyReverseReferenceRule(reverseReferenceRule, changes, context);
                break;

            case OverrideFieldRule overrideFieldRule:
                ApplyOverrideFieldRule(overrideFieldRule, changes, context);
                break;

            default:
                throw new NotSupportedException(
                    $"Unsupported enrichment rule type: {rule.GetType().Name}");
        }
    }

    private static void ApplyReferenceRule(
        ReferenceRule rule,
        IReadOnlyList<AuditChange> changes,
        AuditEnrichmentContext context)
    {
        var loadedEntities = context.GetLoadedEntities(rule.TargetEntityType);

        foreach (var change in changes)
        {
            var (oldFk, newFk) = GetReferenceForeignKeys(change, rule.ForeignKeyPropertyName);

            if (oldFk is not null)
            {
                var oldTarget = loadedEntities.FirstOrDefault(x =>
                    Equals(GetPropertyValue(x, rule.TargetKeyPropertyName), oldFk));

                if (oldTarget is not null)
                {
                    var bag = context.GetBagForChange(change);
                    bag.SetOld(rule.FieldName, rule.ValueSelector(oldTarget));
                }
            }

            if (newFk is not null)
            {
                var newTarget = loadedEntities.FirstOrDefault(x =>
                    Equals(GetPropertyValue(x, rule.TargetKeyPropertyName), newFk));

                if (newTarget is not null)
                {
                    var bag = context.GetBagForChange(change);
                    bag.SetNew(rule.FieldName, rule.ValueSelector(newTarget));
                }
            }
        }
    }

    private static object? GetPropertyValue(object entity, string propertyName)
    {
        var property = entity.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)
                       ?? throw new InvalidOperationException(
                           $"Property '{propertyName}' was not found on type {entity.GetType().FullName}.");

        return property.GetValue(entity);
    }

    private static (object? oldFk, object? newFk) GetReferenceForeignKeys(
        AuditChange change,
        string foreignKeyPropertyName)
    {
        ArgumentNullException.ThrowIfNull(change);
        ArgumentException.ThrowIfNullOrWhiteSpace(foreignKeyPropertyName);

        var entry = change.Entry;
        var currentFk = entry?.Property(foreignKeyPropertyName).CurrentValue;

        var oldAuditValueExists = change.OldValues.TryGetValue(foreignKeyPropertyName, out var oldAuditFk);
        var newAuditValueExists = change.NewValues.TryGetValue(foreignKeyPropertyName, out var newAuditFk);

        return change.State switch
        {
            nameof(EntityState.Added) => (null, newAuditValueExists ? newAuditFk : currentFk),

            nameof(EntityState.Deleted) => (oldAuditValueExists ? oldAuditFk : currentFk, null),

            nameof(EntityState.Modified) => ResolveModifiedForeignKeys(
                oldAuditValueExists,
                oldAuditFk,
                newAuditValueExists,
                newAuditFk,
                currentFk),

            _ => (null, currentFk)
        };
    }

    private static (object? oldFk, object? newFk) ResolveModifiedForeignKeys(
        bool oldAuditValueExists,
        object? oldAuditFk,
        bool newAuditValueExists,
        object? newAuditFk,
        object? currentFk)
    {
        if (oldAuditValueExists || newAuditValueExists)
        {
            return (
                oldAuditValueExists ? oldAuditFk : currentFk,
                newAuditValueExists ? newAuditFk : currentFk
            );
        }

        return (currentFk, currentFk);
    }

    private static void ApplyReverseReferenceRule(
        ReverseReferenceRule rule,
        IReadOnlyList<AuditChange> changes,
        AuditEnrichmentContext context)
    {
        var loadedEntities = context.GetLoadedEntities(rule.TargetEntityType);

        foreach (var change in changes)
        {
            var sourceKey = rule.SourceKeySelector(change);
            if (sourceKey is null)
            {
                continue;
            }

            var related = loadedEntities
                .Where(x => Equals(rule.TargetForeignKeySelector(x), sourceKey))
                .ToList();

            if (related.Count == 0)
            {
                continue;
            }

            var bag = context.GetBagForChange(change);
            rule.Map(change, related, bag);
        }
    }

    private static void ApplyOverrideFieldRule(
        OverrideFieldRule rule,
        IReadOnlyList<AuditChange> changes,
        AuditEnrichmentContext context)
    {
        foreach (var change in changes)
        {
            var value = rule.ValueFactory(change);
            var bag = context.GetBagForChange(change);
            bag.SetNew(rule.FieldName, value);
        }
    }

    private static void ApplyBagsToChanges(AuditEnrichmentContext context)
    {
        foreach (var change in context.Changes)
        {
            var bag = context.GetBagForChange(change);

            foreach (var pair in bag.OldValues)
            {
                change.OldValues[pair.Key] = pair.Value;
            }

            foreach (var pair in bag.NewValues)
            {
                change.NewValues[pair.Key] = pair.Value;
            }
        }
    }

    private sealed class EntityLoadRequest
    {
        public required Type EntityType { get; init; }

        public required string PropertyName { get; init; }

        public required IReadOnlyList<object> Values { get; init; }
    }
}