using Microsoft.EntityFrameworkCore;

namespace AuditLogLens.Enrichment.Internal;

internal interface IAuditEnricher
{
    Task EnrichAsync(
        List<AuditChange> changes,
        DbContext dbContext,
        CancellationToken cancellationToken = default);
}