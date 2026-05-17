using AuditLogLens.Detection.Internal;
using AuditLogLens.Enrichment.Context;
using AuditLogLens.Enrichment.Internal;
using AuditLogLens.Enrichment.Internal.Loading;
using Microsoft.EntityFrameworkCore;

namespace AuditLogLens.Enrichment.Rules;

public sealed class CollectionRule : EnrichmentRule
{
    public required Type ParentEntityType { get; init; }

    public required Type JoinEntityType { get; init; }

    public required Type ItemEntityType { get; init; }

    public required string ParentKeyPropertyName { get; init; }

    public required string JoinParentKeyPropertyName { get; init; }

    public required string JoinItemKeyPropertyName { get; init; }

    public required string ItemKeyPropertyName { get; init; }

    public required string FieldName { get; init; }

    public required Func<object, object?> ItemValueSelector { get; init; }

    internal override EntityLoadRequest? BuildLoadRequest(
        IReadOnlyList<AuditChange> changes,
        AuditEnrichmentContext context)
    {
        var parentChanges = BuildParentChangeIndex(changes, context);
        if (parentChanges.IsEmpty)
        {
            return null;
        }

        var itemKeys = EnrichmentValueCollector.DistinctNonNull(
            GetJoinMatches(context, parentChanges)
                .Select(match => GetJoinItemKey(match.JoinEntry)));

        return itemKeys.Count > 0
            ? new EntityLoadRequest
            {
                EntityType = ItemEntityType,
                PropertyName = ItemKeyPropertyName,
                Values = itemKeys
            }
            : null;
    }

    internal override void Apply(IReadOnlyList<AuditChange> changes, AuditEnrichmentContext context)
    {
        var parentChanges = BuildParentChangeIndex(changes, context);
        if (parentChanges.IsEmpty)
        {
            return;
        }

        var itemEntities = context.GetLoadedEntities(ItemEntityType, ItemKeyPropertyName);
        var itemsByKey = LoadedEntityLookup.OneByKey(itemEntities, GetItemKey);
        if (itemsByKey.Count == 0)
        {
            return;
        }

        foreach (var group in GetCollectionItems(context, parentChanges, itemsByKey)
                     .GroupBy(item => (item.Change, item.Side)))
        {
            var values = group
                .Select(item => item.Value)
                .Where(value => value is not null)
                .Distinct()
                .ToList();

            if (values.Count == 0)
            {
                continue;
            }

            var bag = context.GetBagForChange(group.Key.Change);
            if (group.Key.Side == CollectionSide.Old)
            {
                bag.SetOld(FieldName, values);
            }
            else
            {
                bag.SetNew(FieldName, values);
            }
        }
    }

    private ParentChangeIndex BuildParentChangeIndex(
        IReadOnlyList<AuditChange> changes,
        AuditEnrichmentContext context)
    {
        var byKey = new Dictionary<object, AuditChange>();
        var byEntityReference = new Dictionary<object, AuditChange>(ReferenceEqualityComparer.Instance);

        foreach (var change in changes)
        {
            var key = GetParentKey(change);
            if (key is not null)
            {
                byKey.TryAdd(key, change);
            }

            if (change.Entry is not null)
            {
                byEntityReference.TryAdd(change.Entry.Entity, change);
            }
        }

        AddPreSaveKeysForParentEntries(changes, context, byKey, byEntityReference);

        return new ParentChangeIndex(
            byKey,
            byEntityReference,
            FindJoinToParentNavigationName(context));
    }

    private IEnumerable<CollectionJoinMatch> GetJoinMatches(
        AuditEnrichmentContext context,
        ParentChangeIndex parentChanges)
    {
        foreach (var joinEntry in context.GetTrackedEntries(JoinEntityType))
        {
            if (!TryGetParentChange(joinEntry, parentChanges, out var parentChange))
            {
                continue;
            }

            var side = ResolveSide(parentChange, joinEntry.State);
            if (side is not null)
            {
                yield return new CollectionJoinMatch(joinEntry, parentChange, side.Value);
            }
        }
    }

    private IEnumerable<CollectionItem> GetCollectionItems(
        AuditEnrichmentContext context,
        ParentChangeIndex parentChanges,
        IReadOnlyDictionary<object, object> itemsByKey)
    {
        foreach (var match in GetJoinMatches(context, parentChanges))
        {
            var itemKey = GetJoinItemKey(match.JoinEntry);
            if (itemKey is null || !itemsByKey.TryGetValue(itemKey, out var itemEntity))
            {
                continue;
            }

            yield return new CollectionItem(
                match.Change,
                match.Side,
                ItemValueSelector(itemEntity));
        }
    }

    private void AddPreSaveKeysForParentEntries(
        IReadOnlyList<AuditChange> changes,
        AuditEnrichmentContext context,
        Dictionary<object, AuditChange> byKey,
        IReadOnlyDictionary<object, AuditChange> byEntityReference)
    {
        // Parent changes are post-save entries, while collection join rows are matched from pre-save snapshots.
        // Index both real keys and entity references so generated keys and navigation-based joins can be matched.
        foreach (var parentEntry in changes
                     .Select(change => change.EntityType)
                     .Distinct()
                     .SelectMany(context.GetTrackedEntries))
        {
            if (!byEntityReference.TryGetValue(parentEntry.Entity, out var change))
            {
                continue;
            }

            var key = parentEntry.GetCurrentValue(ParentKeyPropertyName);
            if (key is not null)
            {
                byKey.TryAdd(key, change);
            }
        }
    }

    private bool TryGetParentChange(
        AuditTrackedEntry joinEntry,
        ParentChangeIndex parentChanges,
        out AuditChange parentChange)
    {
        if (parentChanges.JoinToParentNavigationName is not null
            && joinEntry.TryGetReferenceValue(parentChanges.JoinToParentNavigationName, out var parentEntity))
        {
            if (parentEntity is not null
                && parentChanges.ByEntityReference.TryGetValue(parentEntity, out parentChange!))
            {
                return true;
            }
        }

        var parentKey = GetJoinParentKey(joinEntry);
        if (parentKey is not null
            && parentChanges.ByKey.TryGetValue(parentKey, out parentChange!))
        {
            return true;
        }

        parentChange = null!;
        return false;
    }

    private string? FindJoinToParentNavigationName(AuditEnrichmentContext context)
    {
        var joinEntityType = context.DbContext.Model.FindEntityType(JoinEntityType);
        var foreignKey = joinEntityType?
            .GetForeignKeys()
            .FirstOrDefault(foreignKey =>
                foreignKey.DependentToPrincipal is not null
                && foreignKey.Properties.Count == 1
                && foreignKey.Properties[0].Name == JoinParentKeyPropertyName
                && foreignKey.PrincipalEntityType.ClrType.IsAssignableFrom(ParentEntityType));

        return foreignKey?.DependentToPrincipal?.Name;
    }

    private static CollectionSide? ResolveSide(AuditChange parentChange, EntityState joinState)
    {
        return parentChange.State switch
        {
            nameof(EntityState.Added) when joinState is EntityState.Added or EntityState.Unchanged
                => CollectionSide.New,
            nameof(EntityState.Deleted) when joinState is EntityState.Deleted or EntityState.Unchanged
                => CollectionSide.Old,
            nameof(EntityState.Modified) when joinState == EntityState.Added
                => CollectionSide.New,
            nameof(EntityState.Modified) when joinState == EntityState.Deleted
                => CollectionSide.Old,
            _ => null
        };
    }

    private object? GetParentKey(AuditChange change)
    {
        if (change.Entry is null)
        {
            throw new InvalidOperationException(
                $"Collection enrichment for {change.EntityType.FullName} requires AuditChange.Entry to read parent key '{ParentKeyPropertyName}'.");
        }

        if (change.Entry.Metadata.FindProperty(ParentKeyPropertyName) is null)
        {
            throw new InvalidOperationException(
                $"Property '{ParentKeyPropertyName}' was not found on parent entity type {change.Entry.Metadata.ClrType.FullName}.");
        }

        return change.Entry.Property(ParentKeyPropertyName).CurrentValue;
    }

    private object? GetJoinParentKey(AuditTrackedEntry entry)
    {
        return entry.GetCurrentValue(JoinParentKeyPropertyName);
    }

    private object? GetJoinItemKey(AuditTrackedEntry entry)
    {
        return entry.GetCurrentValue(JoinItemKeyPropertyName);
    }

    private object? GetItemKey(object entity)
    {
        return EntityPropertyReader.GetValue(entity, ItemKeyPropertyName);
    }

    private enum CollectionSide
    {
        Old,
        New
    }

    private sealed record CollectionItem(
        AuditChange Change,
        CollectionSide Side,
        object? Value);

    private sealed record CollectionJoinMatch(
        AuditTrackedEntry JoinEntry,
        AuditChange Change,
        CollectionSide Side);

    private sealed record ParentChangeIndex(
        IReadOnlyDictionary<object, AuditChange> ByKey,
        IReadOnlyDictionary<object, AuditChange> ByEntityReference,
        string? JoinToParentNavigationName)
    {
        public bool IsEmpty => ByKey.Count == 0 && ByEntityReference.Count == 0;
    }
}