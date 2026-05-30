namespace AuditLogLens.Enrichment.Context;

/// <summary>
/// Holds values produced by enrichment before they are merged into an audit change.
/// </summary>
/// <remarks>
/// Enrichers write to a bag so all enrichers share one merge point. Values in the bag
/// are copied into the owning audit change after the before-merge phase completes.
/// </remarks>
public sealed class AuditEnrichmentBag
{
    private readonly Dictionary<string, object?> _oldValues = new();
    private readonly Dictionary<string, object?> _newValues = new();
    private readonly Dictionary<string, object?> _extraValues = new();

    /// <summary>
    /// Gets values to add to the change's old values.
    /// </summary>
    public IReadOnlyDictionary<string, object?> OldValues => _oldValues;

    /// <summary>
    /// Gets values to add to the change's new values.
    /// </summary>
    public IReadOnlyDictionary<string, object?> NewValues => _newValues;

    /// <summary>
    /// Gets metadata values to add to the change's extra values.
    /// </summary>
    public IReadOnlyDictionary<string, object?> ExtraValues => _extraValues;

    /// <summary>
    /// Adds or replaces an old value in the bag.
    /// </summary>
    public void SetOld(string key, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _oldValues[key] = value;
    }

    /// <summary>
    /// Adds or replaces a new value in the bag.
    /// </summary>
    public void SetNew(string key, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _newValues[key] = value;
    }

    /// <summary>
    /// Adds or replaces an extra metadata value in the bag.
    /// </summary>
    /// <remarks>
    /// During merge this value replaces any existing value with the same key on the audit change.
    /// </remarks>
    public void SetExtraValue(string key, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _extraValues[key] = value;
    }

    /// <summary>
    /// Adds an extra metadata value to the bag if the key has not already been staged.
    /// </summary>
    /// <remarks>
    /// This only checks values already staged in this bag. It does not check the target audit change.
    /// </remarks>
    public bool TrySetExtraValue(string key, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _extraValues.TryAdd(key, value);
    }

    /// <summary>
    /// Removes an old value from the bag.
    /// </summary>
    public bool RemoveOld(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _oldValues.Remove(key);
    }

    /// <summary>
    /// Removes a new value from the bag.
    /// </summary>
    public bool RemoveNew(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _newValues.Remove(key);
    }

    /// <summary>
    /// Removes an extra metadata value from the bag.
    /// </summary>
    public bool RemoveExtraValue(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _extraValues.Remove(key);
    }

    /// <summary>
    /// Removes a key from old, new, and extra values.
    /// </summary>
    public bool Remove(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var removed = _oldValues.Remove(key);
        removed = _newValues.Remove(key) || removed;
        removed = _extraValues.Remove(key) || removed;

        return removed;
    }

    internal void Clear()
    {
        _oldValues.Clear();
        _newValues.Clear();
        _extraValues.Clear();
    }

    /// <summary>
    /// Gets an old value from the bag.
    /// </summary>
    public bool TryGetOldValue(string key, out object? value)
    {
        return _oldValues.TryGetValue(key, out value);
    }

    /// <summary>
    /// Gets a new value from the bag.
    /// </summary>
    public bool TryGetNewValue(string key, out object? value)
    {
        return _newValues.TryGetValue(key, out value);
    }

    /// <summary>
    /// Gets an extra metadata value from the bag.
    /// </summary>
    public bool TryGetExtraValue(string key, out object? value)
    {
        return _extraValues.TryGetValue(key, out value);
    }

    internal bool HasAnyValues()
    {
        return _oldValues.Count > 0 || _newValues.Count > 0 || _extraValues.Count > 0;
    }
}
