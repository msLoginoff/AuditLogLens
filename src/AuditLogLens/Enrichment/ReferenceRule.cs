namespace AuditLog.Enrichment;

public sealed class ReferenceRule : EnrichmentRule
{
    public required string ForeignKeyPropertyName { get; init; }

    public required string TargetKeyPropertyName { get; init; }

    public required string FieldName { get; init; }

    public required Func<object, object?> ValueSelector { get; init; }
}