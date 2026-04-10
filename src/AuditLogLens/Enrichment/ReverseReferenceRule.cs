namespace AuditLog.Enrichment;

public sealed class ReverseReferenceRule : EnrichmentRule
{
    public required Func<AuditChange, object?> SourceKeySelector { get; init; }

    public required string TargetForeignKeyPropertyName { get; init; }

    public required Func<object, object?> TargetForeignKeySelector { get; init; }

    public required Action<AuditChange, IReadOnlyList<object>, AuditEnrichmentBag> Map { get; init; }
}