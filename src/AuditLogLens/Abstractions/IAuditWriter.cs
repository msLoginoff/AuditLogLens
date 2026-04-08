using Microsoft.EntityFrameworkCore;

namespace AuditLog.Abstractions;

public interface IAuditWriter
{
    Task WriteAsync(
        IReadOnlyList<AuditChange> changes,
        DbContext dbContext,
        CancellationToken cancellationToken = default);
}