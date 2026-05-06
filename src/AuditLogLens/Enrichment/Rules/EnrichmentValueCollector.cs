namespace AuditLogLens.Enrichment;

internal static class EnrichmentValueCollector
{
    public static IReadOnlyList<object> DistinctNonNull(IEnumerable<object?> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        return values
            .Where(x => x is not null)
            .Cast<object>()
            .Distinct()
            .ToList();
    }
}