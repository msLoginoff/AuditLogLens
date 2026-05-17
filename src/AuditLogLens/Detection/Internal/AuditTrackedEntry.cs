using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace AuditLogLens.Detection.Internal;

internal sealed class AuditTrackedEntry
{
    private readonly Dictionary<string, object?> _currentValues;
    private readonly Dictionary<string, object?> _referenceValues;

    public AuditTrackedEntry(EntityEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        Entry = entry;
        Entity = entry.Entity;
        EntityType = entry.Metadata.ClrType;
        State = entry.State;
        _currentValues = entry.Properties.ToDictionary(
            property => property.Metadata.Name,
            property => property.CurrentValue);
        _referenceValues = entry.References.ToDictionary(
            reference => reference.Metadata.Name,
            reference => reference.CurrentValue);
    }

    public EntityEntry Entry { get; }

    public object Entity { get; }

    public Type EntityType { get; }

    public EntityState State { get; }

    public object? GetCurrentValue(string propertyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        if (_currentValues.TryGetValue(propertyName, out var value))
        {
            return value;
        }

        throw new InvalidOperationException(
            $"Property '{propertyName}' was not found on tracked entity type {EntityType.FullName}.");
    }

    public bool TryGetReferenceValue(string navigationName, out object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(navigationName);

        return _referenceValues.TryGetValue(navigationName, out value);
    }
}