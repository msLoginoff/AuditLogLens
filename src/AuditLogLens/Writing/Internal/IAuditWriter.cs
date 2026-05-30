using AuditLogLens.Changes;
using AuditLogLens.Pipeline;
using Microsoft.EntityFrameworkCore;

namespace AuditLogLens.Writing.Internal;

internal interface IAuditWriter
{
    Task WriteAsync(
        IReadOnlyList<AuditChange> changes,
        DbContext dbContext,
        AuditSaveBehavior saveBehavior,
        CancellationToken cancellationToken = default);
}
