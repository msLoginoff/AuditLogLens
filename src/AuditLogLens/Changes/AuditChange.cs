using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace AuditLogLens.Changes;

/// <summary>
/// Represents one audit change before it is mapped to the application's audit record.
/// </summary>
/// <remarks>
/// Changes may come from EF Core detection or from manual application code. Enrichers may
/// add values to <see cref="OldValues"/>, <see cref="NewValues"/>, and <see cref="ExtraValues"/>.
/// </remarks>
public sealed class AuditChange
{
    private readonly Dictionary<string, object?> _syntheticKeyValues = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the type used to resolve enrichment rules for this change.
    /// </summary>
    /// <remarks>
    /// EF changes use the EF entity CLR type. Manual changes may use a DTO or payload type.
    /// </remarks>
    public required Type EntityType { get; init; }

    /// <summary>
    /// Gets or sets the logical row key or event key for the audited subject.
    /// </summary>
    public object? EntityId { get; set; }

    /// <summary>
    /// Gets the logical state of the audited subject.
    /// </summary>
    public required AuditChangeState State { get; init; }

    /// <summary>
    /// Gets the audit table or event group name. If omitted, mappers may use <see cref="EntityType"/>.
    /// </summary>
    public string? TableName { get; init; }

    /// <summary>
    /// Gets metadata for mappers and enrichers. These values are not old/new field values.
    /// </summary>
    public Dictionary<string, object?> ExtraValues { get; } = new();

    /// <summary>
    /// Gets values from before the change.
    /// </summary>
    public Dictionary<string, object?> OldValues { get; } = new();

    /// <summary>
    /// Gets values from after the change.
    /// </summary>
    public Dictionary<string, object?> NewValues { get; } = new();

    /// <summary>
    /// Gets or sets whether this change has been updated after the main EF save.
    /// </summary>
    public bool IsAfterSavePhase { get; set; }

    /// <summary>
    /// Gets the EF Core entry for automatic changes. This is <see langword="null"/> for manual changes.
    /// </summary>
    public EntityEntry? Entry { get; init; }

    /// <summary>
    /// Gets the original entity or manual source object, when one is available.
    /// </summary>
    public object? Entity { get; init; }

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

    /// <summary>
    /// Adds or replaces an extra metadata value.
    /// </summary>
    /// <param name="key">The metadata key.</param>
    /// <param name="value">The metadata value.</param>
    public void SetExtraValue(string key, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ExtraValues[key] = value;
    }

    /// <summary>
    /// Adds an extra metadata value if the key is not already present.
    /// </summary>
    /// <returns><see langword="true"/> if the value was added; otherwise, <see langword="false"/>.</returns>
    public bool TrySetExtraValue(string key, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return ExtraValues.TryAdd(key, value);
    }

    /// <summary>
    /// Gets an extra metadata value by key.
    /// </summary>
    public bool TryGetExtraValue(string key, out object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return ExtraValues.TryGetValue(key, out value);
    }

    /// <summary>
    /// Gets an extra metadata value when the stored value has the requested type.
    /// </summary>
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
