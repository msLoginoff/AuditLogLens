using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace AuditLogLens;

public sealed class AuditChange
{
    private readonly Dictionary<string, object?> _syntheticKeyValues = new(StringComparer.Ordinal);

    public required Type EntityType { get; init; }

    public object? EntityId { get; set; }

    public required string State { get; init; }

    public string? TableName { get; init; }

    public Dictionary<string, object?> ExtraValues { get; } = new();

    public Dictionary<string, object?> OldValues { get; } = new();

    public Dictionary<string, object?> NewValues { get; } = new();

    public bool IsAfterSavePhase { get; set; }

    public EntityEntry? Entry { get; init; }

    internal void SetSyntheticKeyValue(string key, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _syntheticKeyValues[key] = value;
    }

    internal bool TryGetSyntheticKeyValue(string key, out object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _syntheticKeyValues.TryGetValue(key, out value);
    }

    public void SetExtraValue(string key, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ExtraValues[key] = value;
    }

    public bool TryGetExtraValue(string key, out object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return ExtraValues.TryGetValue(key, out value);
    }

    public bool TryGetExtraValue<TValue>(
        string key,
        [MaybeNullWhen(false)] out TValue value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (ExtraValues.TryGetValue(key, out var rawValue)
            && rawValue is TValue typedValue)
        {
            value = typedValue;
            return true;
        }

        value = default;
        return false;
    }
}