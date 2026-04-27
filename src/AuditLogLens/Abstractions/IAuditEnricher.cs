using Microsoft.EntityFrameworkCore;

namespace AuditLog.Abstractions;

public interface IAuditEnricher
{
    Task EnrichAsync(
        List<AuditChange> changes,
        DbContext dbContext,
        CancellationToken cancellationToken = default);
}