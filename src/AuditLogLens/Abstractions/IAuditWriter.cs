using Microsoft.EntityFrameworkCore;

namespace AuditLog.Abstractions;

public interface IAuditWriter
{
    Task WriteAsync(
        List<AuditChange> changes,
        DbContext dbContext,
        CancellationToken cancellationToken = default);
}