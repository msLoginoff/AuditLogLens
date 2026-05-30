using AuditLogLens.Changes;
using AuditLogLens.Enrichment.Context;
using AuditLogLens.Enrichment.Internal.Loading;

namespace AuditLogLens.Enrichment.Rules;

/// <summary>
/// Writes a computed value into the new-values side of each audit bag.
/// </summary>
/// <remarks>
/// This rule does not preload data. It is intended for simple values that can be
/// calculated from the current <see cref="AuditChange"/>.
/// </remarks>
public sealed class OverrideFieldRule : EnrichmentRule
{
    public required string FieldName { get; init; }

    public required Func<AuditChange, object?> ValueFactory { get; init; }

    internal override EntityLoadRequest? BuildLoadRequest(
        IReadOnlyList<AuditChange> changes,
        AuditEnrichmentContext context)
        => null;

    internal override void Apply(IReadOnlyList<AuditChange> changes, AuditEnrichmentContext context)
    {
        foreach (var change in changes)
            context.GetBagFor(change).SetNew(FieldName, ValueFactory(change));
    }
}
