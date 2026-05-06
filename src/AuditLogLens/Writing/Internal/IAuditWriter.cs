using Microsoft.EntityFrameworkCore;

namespace AuditLogLens.Writing.Internal;

internal interface IAuditWriter
{
    Task WriteAsync(
        IReadOnlyList<AuditChange> changes,
        DbContext dbContext,
        CancellationToken cancellationToken = default);
}