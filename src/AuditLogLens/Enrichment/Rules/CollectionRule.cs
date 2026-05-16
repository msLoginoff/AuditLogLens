using AuditLogLens.Detection.Internal;
using AuditLogLens.Enrichment.Context;
using AuditLogLens.Enrichment.Internal;
using AuditLogLens.Enrichment.Internal.Loading;
using Microsoft.EntityFrameworkCore;

namespace AuditLogLens.Enrichment.Rules;

public sealed class CollectionRule : EnrichmentRule
{
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
        var parentKeys = GetParentChangesByKey(changes);
        if (parentKeys.Count == 0)
        {
            return null;
        }

        var itemKeys = EnrichmentValueCollector.DistinctNonNull(
            GetRelevantJoinEntries(context, parentKeys)
                .Select(GetJoinItemKey));

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
        var parentKeys = GetParentChangesByKey(changes);
        if (parentKeys.Count == 0)
        {
            return;
        }

        var itemEntities = context.GetLoadedEntities(ItemEntityType, ItemKeyPropertyName);
        var itemsByKey = LoadedEntityLookup.OneByKey(itemEntities, GetItemKey);
        if (itemsByKey.Count == 0)
        {
            return;
        }

        foreach (var group in GetCollectionItems(context, parentKeys, itemsByKey)
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

    private Dictionary<object, AuditChange> GetParentChangesByKey(IReadOnlyList<AuditChange> changes)
    {
        var result = new Dictionary<object, AuditChange>();

        foreach (var change in changes)
        {
            var key = GetParentKey(change);
            if (key is not null)
            {
                result.TryAdd(key, change);
            }
        }

        return result;
    }

    private IEnumerable<AuditTrackedEntry> GetRelevantJoinEntries(
        AuditEnrichmentContext context,
        IReadOnlyDictionary<object, AuditChange> parentKeys)
    {
        foreach (var entry in context.GetTrackedEntries(JoinEntityType))
        {
            var parentKey = GetJoinParentKey(entry);
            if (parentKey is null || !parentKeys.TryGetValue(parentKey, out var parentChange))
            {
                continue;
            }

            if (ResolveSide(parentChange, entry.State) is not null)
            {
                yield return entry;
            }
        }
    }

    private IEnumerable<CollectionItem> GetCollectionItems(
        AuditEnrichmentContext context,
        IReadOnlyDictionary<object, AuditChange> parentKeys,
        IReadOnlyDictionary<object, object> itemsByKey)
    {
        foreach (var joinEntry in GetRelevantJoinEntries(context, parentKeys))
        {
            var parentKey = GetJoinParentKey(joinEntry);
            if (parentKey is null || !parentKeys.TryGetValue(parentKey, out var change))
            {
                continue;
            }

            var side = ResolveSide(change, joinEntry.State);
            if (side is null)
            {
                continue;
            }

            var itemKey = GetJoinItemKey(joinEntry);
            if (itemKey is null || !itemsByKey.TryGetValue(itemKey, out var itemEntity))
            {
                continue;
            }

            yield return new CollectionItem(
                change,
                side.Value,
                ItemValueSelector(itemEntity));
        }
    }

    private CollectionSide? ResolveSide(AuditChange parentChange, EntityState joinState)
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
}