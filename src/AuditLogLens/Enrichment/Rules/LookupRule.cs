using AuditLogLens.Changes;
using AuditLogLens.Enrichment.Context;
using AuditLogLens.Enrichment.Internal.Loading;

namespace AuditLogLens.Enrichment.Rules;

/// <summary>
/// Preload-only enrichment rule. Each change can contribute multiple keys via
/// <see cref="KeysSelector"/>; the loader batches them across all changes
/// and deposits the loaded entities into the enrichment context. Enrichers
/// consume the data through <c>context.GetLoaded&lt;T&gt;(...)</c> in their hooks.
///
/// Unlike <see cref="ReferenceRule"/> and <see cref="ReverseReferenceRule"/>,
/// this rule does not write anything to per-change bags itself — projection
/// and field population are entirely up to the consuming enricher.
/// </summary>
public sealed class LookupRule : EnrichmentRule
{
    public required Type TargetEntityType { get; init; }

    public required string TargetPropertyName { get; init; }

    public required Func<AuditChange, IEnumerable<object?>> KeysSelector { get; init; }

    public IReadOnlyList<string> IncludePaths { get; init; } = [];

    internal override EntityLoadRequest? BuildLoadRequest(
        IReadOnlyList<AuditChange> changes,
        AuditEnrichmentContext context)
    {
        var values = EnrichmentValueCollector.DistinctNonNull(
            changes.SelectMany(change => KeysSelector(change) ?? Array.Empty<object?>()));

        return values.Count > 0
            ? new EntityLoadRequest
            {
                EntityType = TargetEntityType,
                PropertyName = TargetPropertyName,
                Values = values,
                IncludePaths = IncludePaths
            }
            : null;
    }

    internal override void Apply(IReadOnlyList<AuditChange> changes, AuditEnrichmentContext context)
    {
        // Intentionally empty: loaded entities live in context.GetLoadedEntities(...)
        // for enrichers to consume in their before/after-merge hooks.
    }
}