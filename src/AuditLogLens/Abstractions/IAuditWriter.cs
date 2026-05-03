using Microsoft.EntityFrameworkCore;

namespace AuditLogLens.Abstractions;

public interface IAuditWriter
{
    Task WriteAsync(
        IReadOnlyList<AuditChange> changes,
        DbContext dbContext,
        CancellationToken cancellationToken = default);
}