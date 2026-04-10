namespace AuditLog.Enrichment;

public abstract class EnrichmentRule
{
    public required Type SourceEntityType { get; init; }

    public required Type TargetEntityType { get; init; }

    public string? Description { get; init; }
}