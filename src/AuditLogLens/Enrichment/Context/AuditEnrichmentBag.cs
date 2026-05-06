namespace AuditLogLens.Enrichment;

public sealed class AuditEnrichmentBag
{
    private readonly Dictionary<string, object?> _oldValues = new();
    private readonly Dictionary<string, object?> _newValues = new();
    private readonly Dictionary<string, object?> _extraValues = new();

    public IReadOnlyDictionary<string, object?> OldValues => _oldValues;

    public IReadOnlyDictionary<string, object?> NewValues => _newValues;

    public IReadOnlyDictionary<string, object?> ExtraValues => _extraValues;

    public void SetOld(string key, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _oldValues[key] = value;
    }

    public void SetNew(string key, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _newValues[key] = value;
    }

    public void SetExtraValue(string key, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _extraValues[key] = value;
    }

    public bool TryGetOldValue(string key, out object? value)
    {
        return _oldValues.TryGetValue(key, out value);
    }

    public bool TryGetNewValue(string key, out object? value)
    {
        return _newValues.TryGetValue(key, out value);
    }

    public bool TryGetExtraValue(string key, out object? value)
    {
        return _extraValues.TryGetValue(key, out value);
    }

    public bool HasAnyValues()
    {
        return _oldValues.Count > 0 || _newValues.Count > 0 || _extraValues.Count > 0;
    }
}