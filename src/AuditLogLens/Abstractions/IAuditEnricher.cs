using Microsoft.EntityFrameworkCore;

namespace AuditLogLens.Abstractions;

public interface IAuditEnricher
{
    Task EnrichAsync(
        List<AuditChange> changes,
        DbContext dbContext,
        CancellationToken cancellationToken = default);
}