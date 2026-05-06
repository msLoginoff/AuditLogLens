using AuditLogLens.Enrichment.Context;
using AuditLogLens.Enrichment.Internal.Loading;

namespace AuditLogLens.Enrichment.Rules;

public sealed class OverrideFieldRule : EnrichmentRule
{
    public required string FieldName { get; init; }

    public required Func<AuditChange, object?> ValueFactory { get; init; }

    internal override EntityLoadRequest? BuildLoadRequest(IReadOnlyList<AuditChange> changes) => null;

    internal override void Apply(IReadOnlyList<AuditChange> changes, AuditEnrichmentContext context)
    {
        foreach (var change in changes)
            context.GetBagForChange(change).SetNew(FieldName, ValueFactory(change));
    }
}