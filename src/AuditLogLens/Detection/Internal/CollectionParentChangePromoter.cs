using AuditLogLens.Enrichment.Internal.Planning;
using AuditLogLens.Enrichment.Rules;
using Microsoft.EntityFrameworkCore;

namespace AuditLogLens.Detection.Internal;

internal sealed class CollectionParentChangePromoter
{
    private readonly AuditEnrichmentPlanResolver _planResolver;

    public CollectionParentChangePromoter(AuditEnrichmentPlanResolver planResolver)
    {
        _planResolver = planResolver;
    }

    public void Promote(
        DbContext dbContext,
        AuditSaveContext saveContext,
        Func<CollectionRule, bool> shouldPromoteRule)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(saveContext);
        ArgumentNullException.ThrowIfNull(shouldPromoteRule);

        foreach (var rule in _planResolver.ResolveCollectionRules(dbContext))
        {
            if (!shouldPromoteRule(rule))
            {
                continue;
            }

            var joinToParentNavigationName = rule.FindJoinToParentNavigationName(dbContext);
            var canPromoteByScalarParentKey = CanPromoteByScalarParentKey(dbContext, rule);
            foreach (var joinEntry in GetChangedJoinEntries(saveContext, rule))
            {
                var parent = ResolveParent(
                    rule,
                    joinEntry,
                    saveContext.TrackedEntries,
                    joinToParentNavigationName,
                    canPromoteByScalarParentKey);
                var parentKey = parent?.Key;
                if (parentKey is null || HasExistingParentChange(saveContext, rule, parentKey))
                {
                    continue;
                }

                saveContext.PreSaveChanges.Add(CreateSyntheticParentChange(
                    rule,
                    parentKey,
                    parent?.Entity));
            }
        }
    }

    private static IEnumerable<AuditTrackedEntry> GetChangedJoinEntries(
        AuditSaveContext saveContext,
        CollectionRule rule)
    {
        return saveContext.TrackedEntries
            .Where(entry => rule.JoinEntityType.IsAssignableFrom(entry.EntityType))
            .Where(entry => entry.State is EntityState.Added or EntityState.Deleted);
    }

    private static SyntheticParent? ResolveParent(
        CollectionRule rule,
        AuditTrackedEntry joinEntry,
        IReadOnlyCollection<AuditTrackedEntry> trackedEntries,
        string? joinToParentNavigationName,
        bool canPromoteByScalarParentKey)
    {
        if (joinToParentNavigationName is not null
            && joinEntry.TryGetReferenceValue(joinToParentNavigationName, out var parentEntity)
            && parentEntity is not null
            && rule.ParentEntityType.IsInstanceOfType(parentEntity))
        {
            var parentEntry = joinEntry.Entry.Context.Entry(parentEntity);
            if (parentEntry.State is not EntityState.Detached)
            {
                // Keep the parent object so application enrichers can read domain-specific metadata.
                return new SyntheticParent(
                    parentEntry.Property(rule.ParentKeyPropertyName).CurrentValue,
                    parentEntity);
            }
        }

        var trackedParent = ResolveTrackedParentByScalarKey(rule, joinEntry, trackedEntries);
        if (trackedParent is not null)
        {
            return trackedParent;
        }

        return canPromoteByScalarParentKey
            ? new SyntheticParent(joinEntry.GetCurrentValue(rule.JoinParentKeyPropertyName), null)
            : null;
    }

    private static SyntheticParent? ResolveTrackedParentByScalarKey(
        CollectionRule rule,
        AuditTrackedEntry joinEntry,
        IEnumerable<AuditTrackedEntry> trackedEntries)
    {
        var parentKey = joinEntry.GetCurrentValue(rule.JoinParentKeyPropertyName);
        if (parentKey is null)
        {
            return null;
        }

        foreach (var trackedEntry in trackedEntries)
        {
            if (!rule.ParentEntityType.IsAssignableFrom(trackedEntry.EntityType))
            {
                continue;
            }

            var trackedParentKey = trackedEntry.GetCurrentValue(rule.ParentKeyPropertyName);
            if (Equals(trackedParentKey, parentKey))
            {
                return new SyntheticParent(parentKey, trackedEntry.Entity);
            }
        }

        return null;
    }

    private static bool CanPromoteByScalarParentKey(DbContext dbContext, CollectionRule rule)
    {
        var joinEntityType = dbContext.Model.FindEntityType(rule.JoinEntityType);
        var foreignKey = joinEntityType?
            .GetForeignKeys()
            .FirstOrDefault(foreignKey =>
                foreignKey.Properties.Count == 1
                && foreignKey.Properties[0].Name == rule.JoinParentKeyPropertyName);

        return foreignKey?.PrincipalEntityType.ClrType == rule.ParentEntityType;
    }

    private static bool HasExistingParentChange(
        AuditSaveContext saveContext,
        CollectionRule rule,
        object parentKey)
    {
        return saveContext.PreSaveChanges.Any(change =>
            rule.ParentEntityType.IsAssignableFrom(change.EntityType)
            && TryGetChangeParentKey(change, rule, out var existingKey)
            && Equals(existingKey, parentKey));
    }

    private static bool TryGetChangeParentKey(
        AuditChange change,
        CollectionRule rule,
        out object? parentKey)
    {
        if (change.Entry?.Metadata.FindProperty(rule.ParentKeyPropertyName) is not null)
        {
            parentKey = change.Entry.Property(rule.ParentKeyPropertyName).CurrentValue;
            return true;
        }

        return change.TryGetSyntheticKeyValue(rule.ParentKeyPropertyName, out parentKey);
    }

    private static AuditChange CreateSyntheticParentChange(
        CollectionRule rule,
        object parentKey,
        object? parentEntity)
    {
        var change = new AuditChange
        {
            EntityType = rule.ParentEntityType,
            EntityId = parentKey,
            Entity = parentEntity,
            State = AuditChangeState.Modified,
            TableName = rule.ParentEntityType.Name,
            IsAfterSavePhase = false
        };

        change.SetSyntheticKeyValue(rule.ParentKeyPropertyName, parentKey);

        return change;
    }

    private sealed record SyntheticParent(
        object? Key,
        object? Entity);
}
