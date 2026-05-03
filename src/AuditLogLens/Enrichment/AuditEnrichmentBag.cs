namespace AuditLogLens.Enrichment;

public sealed class AuditEnrichmentBag
{
    private readonly Dictionary<string, object?> _oldValues = new();
    private readonly Dictionary<string, object?> _newValues = new();

    public IReadOnlyDictionary<string, object?> OldValues => _oldValues;

    public IReadOnlyDictionary<string, object?> NewValues => _newValues;

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

    public bool TryGetOldValue(string key, out object? value)
    {
        return _oldValues.TryGetValue(key, out value);
    }

    public bool TryGetNewValue(string key, out object? value)
    {
        return _newValues.TryGetValue(key, out value);
    }

    public bool HasAnyValues()
    {
        return _oldValues.Count > 0 || _newValues.Count > 0;
    }
}