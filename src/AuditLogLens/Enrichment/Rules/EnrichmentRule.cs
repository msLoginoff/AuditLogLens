using AuditLogLens.Changes;
using AuditLogLens.Enrichment.Context;
using AuditLogLens.Enrichment.Internal.Loading;

namespace AuditLogLens.Enrichment.Rules;

/// <summary>
/// Base type for enrichment rules that can preload data and write values to audit bags.
/// </summary>
/// <remarks>
/// This is an advanced extension point. Most consumers should configure rules through the
/// fluent methods in <c>AuditLogLens.Enrichment.Extensions</c> instead of constructing rule
/// objects directly.
/// </remarks>
public abstract class EnrichmentRule
{
    /// <summary>
    /// Gets an optional diagnostic description for the rule.
    /// </summary>
    public string? Description { get; init; }

    internal abstract EntityLoadRequest? BuildLoadRequest(
        IReadOnlyList<AuditChange> changes,
        AuditEnrichmentContext context);

    internal abstract void Apply(IReadOnlyList<AuditChange> changes, AuditEnrichmentContext context);
}
