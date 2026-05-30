using AuditLogLens.Enrichment.Context;

namespace AuditLogLens.Enrichment;

/// <summary>
/// Represents an application enricher that can add or reshape audit values.
/// </summary>
public interface IAuditEntityEnricher
{
    /// <summary>
    /// Determines whether this enricher handles the specified entity type.
    /// </summary>
    bool CanHandle(Type entityType);

    /// <summary>
    /// Declares enrichment rules and lookup requirements for this enricher.
    /// </summary>
    void Configure(IAuditEnrichmentPlanBuilder builder);

    /// <summary>
    /// Runs before staged enrichment values are merged into audit changes.
    /// </summary>
    Task ApplyBeforeMergeAsync(AuditEnrichmentContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs after staged enrichment values are merged into audit changes.
    /// </summary>
    Task ApplyAfterMergeAsync(AuditEnrichmentContext context, CancellationToken cancellationToken = default);
}
