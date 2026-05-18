using AuditLogLens.Detection.Internal;
using Microsoft.EntityFrameworkCore;

namespace AuditLogLens.Enrichment.Context;

public sealed class AuditEnrichmentContext
{
    private readonly Dictionary<AuditChange, AuditEnrichmentBag> _bags = new();
    private readonly Dictionary<Type, IReadOnlyList<AuditChange>> _changesByEntityType;
    private readonly Dictionary<(Type EntityType, string PropertyName), List<object>> _loadedEntities = new();
    private readonly IReadOnlyList<AuditTrackedEntry> _trackedEntries;
    private readonly Dictionary<Type, IReadOnlyList<AuditTrackedEntry>> _trackedEntriesByEntityType = new();

    internal AuditEnrichmentContext(
        IReadOnlyList<AuditChange> changes,
        DbContext dbContext,
        IReadOnlyList<AuditTrackedEntry> trackedEntries)
    {
        ArgumentNullException.ThrowIfNull(changes);
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(trackedEntries);

        Changes = changes;
        DbContext = dbContext;
        _changesByEntityType = changes
            .GroupBy(x => x.EntityType)
            .ToDictionary(x => x.Key, x => (IReadOnlyList<AuditChange>)x.ToList());
        _trackedEntries = trackedEntries;

        foreach (var change in changes)
        {
            _bags[change] = new AuditEnrichmentBag();
        }
    }

    public IReadOnlyList<AuditChange> Changes { get; }

    public DbContext DbContext { get; }

    internal IReadOnlyList<Type> EntityTypes => _changesByEntityType.Keys.ToList();

    public IReadOnlyList<AuditChange> GetChangesOfType(Type entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        return _changesByEntityType.TryGetValue(entityType, out var changes)
            ? changes
            : [];
    }

    internal IReadOnlyList<AuditTrackedEntry> GetTrackedEntries(Type entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);

        if (_trackedEntriesByEntityType.TryGetValue(entityType, out var entries))
        {
            return entries;
        }

        entries = _trackedEntries
            .Where(entry => entityType.IsAssignableFrom(entry.EntityType))
            .ToList();

        _trackedEntriesByEntityType[entityType] = entries;
        return entries;
    }

    public AuditEnrichmentBag GetBagForChange(AuditChange change)
    {
        ArgumentNullException.ThrowIfNull(change);
        return _bags[change];
    }

    internal void SetLoadedEntities(
        Type entityType,
        string propertyName,
        IEnumerable<object> entities)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        ArgumentNullException.ThrowIfNull(entities);

        _loadedEntities[(entityType, propertyName)] = entities.ToList();
    }

    public IReadOnlyList<object> GetLoadedEntities(Type entityType, string propertyName)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        return _loadedEntities.TryGetValue((entityType, propertyName), out var entities)
            ? entities
            : Array.Empty<object>();
    }

    public IReadOnlyList<T> GetLoaded<T>(string propertyName) where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        var entities = GetLoadedEntities(typeof(T), propertyName);
        if (entities.Count == 0)
        {
            return Array.Empty<T>();
        }

        var result = new List<T>(entities.Count);
        foreach (var entity in entities)
        {
            if (entity is T typed)
            {
                result.Add(typed);
            }
        }

        return result;
    }

    internal void MergeBagsToChanges()
    {
        foreach (var change in Changes)
        {
            var bag = _bags[change];

            foreach (var pair in bag.OldValues)
            {
                change.OldValues[pair.Key] = pair.Value;
            }

            foreach (var pair in bag.NewValues)
            {
                change.NewValues[pair.Key] = pair.Value;
            }

            foreach (var pair in bag.ExtraValues)
            {
                change.SetExtraValue(pair.Key, pair.Value);
            }

            bag.Clear();
        }
    }
}