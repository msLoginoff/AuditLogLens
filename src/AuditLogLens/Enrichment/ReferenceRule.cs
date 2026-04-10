namespace AuditLog.Enrichment;

public sealed class ReferenceRule : EnrichmentRule
{
    public required Func<AuditChange, object?> ForeignKeySelector { get; init; }

    public required string TargetKeyPropertyName { get; init; }

    public required Func<object, object?> TargetKeySelector { get; init; }

    public required Action<AuditChange, object, AuditEnrichmentBag> Map { get; init; }
}