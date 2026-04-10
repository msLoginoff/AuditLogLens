using Microsoft.EntityFrameworkCore;

namespace AuditLog.Enrichment;

public sealed class AuditEnrichmentContext
{
    private readonly Dictionary<AuditChange, AuditEnrichmentBag> _bags = new();
    private readonly Dictionary<Type, List<object>> _loadedEntities = new();

    public AuditEnrichmentContext(
        IReadOnlyList<AuditChange> changes,
        DbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(changes);
        ArgumentNullException.ThrowIfNull(dbContext);

        Changes = changes;
        DbContext = dbContext;

        foreach (var change in changes)
        {
            _bags[change] = new AuditEnrichmentBag();
        }
    }

    public IReadOnlyList<AuditChange> Changes { get; }

    public DbContext DbContext { get; }

    public IEnumerable<AuditChange> GetChangesOfType(Type entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        return Changes.Where(x => x.EntityType == entityType);
    }

    public AuditEnrichmentBag GetBagForChange(AuditChange change)
    {
        ArgumentNullException.ThrowIfNull(change);
        return _bags[change];
    }

    public void SetLoadedEntities(Type entityType, IEnumerable<object> entities)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        ArgumentNullException.ThrowIfNull(entities);

        _loadedEntities[entityType] = entities.ToList();
    }

    public IReadOnlyList<object> GetLoadedEntities(Type entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);

        return _loadedEntities.TryGetValue(entityType, out var entities)
            ? entities
            : Array.Empty<object>();
    }
}