namespace AuditLogLens.Enrichment.Loading;

internal sealed class EntityLoadRequest
{
    public required Type EntityType { get; init; }

    public required string PropertyName { get; init; }

    public required IReadOnlyList<object> Values { get; init; }
}