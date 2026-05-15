using AuditLogLens.Detection.Internal;
using Microsoft.EntityFrameworkCore;

namespace AuditLogLens.Enrichment.Internal;

internal interface IAuditEnricher
{
    Task EnrichAsync(
        List<AuditChange> changes,
        DbContext dbContext,
        IReadOnlyList<AuditTrackedEntry> trackedEntries,
        CancellationToken cancellationToken = default);
}