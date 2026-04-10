namespace AuditLog.Enrichment;

public sealed class OverrideFieldRule : EnrichmentRule
{
    public required string FieldName { get; init; }

    public required Func<AuditChange, object?> ValueFactory { get; init; }
}