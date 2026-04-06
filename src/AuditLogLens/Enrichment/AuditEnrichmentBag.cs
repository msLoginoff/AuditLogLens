namespace AuditLog.Enrichment;

public sealed class AuditEnrichmentBag
{
    private readonly Dictionary<string, object?> _values = new();

    public IReadOnlyDictionary<string, object?> Values => _values;

    public void Set(string key, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _values[key] = value;
    }

    public bool TryGetValue(string key, out object? value)
    {
        return _values.TryGetValue(key, out value);
    }

    public bool HasValues()
    {
        return _values.Count > 0;
    }
}