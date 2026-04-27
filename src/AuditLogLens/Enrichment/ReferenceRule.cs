using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace AuditLog.Enrichment;

public sealed class ReferenceRule : EnrichmentRule
{
    public required Type TargetEntityType { get; init; }

    public required string ForeignKeyPropertyName { get; init; }

    public required string TargetKeyPropertyName { get; init; }

    public required string FieldName { get; init; }

    public required Func<object, object?> ValueSelector { get; init; }

    internal override EntityLoadRequest? BuildLoadRequest(IReadOnlyList<AuditChange> changes)
    {
        var values = changes
            .SelectMany(change =>
            {
                var (oldFk, newFk) = ResolveForeignKeys(change);
                return new[] { oldFk, newFk };
            })
            .Where(x => x is not null)
            .Cast<object>()
            .Distinct()
            .ToList();

        return values.Count > 0
            ? new EntityLoadRequest
            {
                EntityType = TargetEntityType,
                PropertyName = TargetKeyPropertyName,
                Values = values
            }
            : null;
    }

    internal override void Apply(IReadOnlyList<AuditChange> changes, AuditEnrichmentContext context)
    {
        var loadedEntities = context.GetLoadedEntities(TargetEntityType);

        foreach (var change in changes)
        {
            var (oldFk, newFk) = ResolveForeignKeys(change);

            if (oldFk is not null)
            {
                var oldTarget = loadedEntities.FirstOrDefault(x => Equals(GetKeyValue(x), oldFk));
                if (oldTarget is not null)
                    context.GetBagForChange(change).SetOld(FieldName, ValueSelector(oldTarget));
            }

            if (newFk is not null)
            {
                var newTarget = loadedEntities.FirstOrDefault(x => Equals(GetKeyValue(x), newFk));
                if (newTarget is not null)
                    context.GetBagForChange(change).SetNew(FieldName, ValueSelector(newTarget));
            }
        }
    }

    private (object? oldFk, object? newFk) ResolveForeignKeys(AuditChange change)
    {
        var currentFk = change.Entry?.Property(ForeignKeyPropertyName).CurrentValue;

        var hasOld = change.OldValues.TryGetValue(ForeignKeyPropertyName, out var oldFk);
        var hasNew = change.NewValues.TryGetValue(ForeignKeyPropertyName, out var newFk);

        return change.State switch
        {
            nameof(EntityState.Added) => (null, hasNew ? newFk : currentFk),
            nameof(EntityState.Deleted) => (hasOld ? oldFk : currentFk, null),
            nameof(EntityState.Modified) => ResolveModified(hasOld, oldFk, hasNew, newFk, currentFk),
            _ => (null, currentFk)
        };
    }

    private static (object? oldFk, object? newFk) ResolveModified(
        bool hasOld, object? oldFk,
        bool hasNew, object? newFk,
        object? currentFk)
    {
        if (hasOld || hasNew)
            return (hasOld ? oldFk : currentFk, hasNew ? newFk : currentFk);

        return (currentFk, currentFk);
    }

    private object? GetKeyValue(object entity)
    {
        var property = entity.GetType()
                           .GetProperty(TargetKeyPropertyName, BindingFlags.Public | BindingFlags.Instance)
                       ?? throw new InvalidOperationException(
                           $"Property '{TargetKeyPropertyName}' was not found on type {entity.GetType().FullName}.");

        return property.GetValue(entity);
    }
}