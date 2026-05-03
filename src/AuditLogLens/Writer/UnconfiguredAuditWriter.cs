using AuditLog.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace AuditLog.Writer;

public sealed class UnconfiguredAuditWriter : IAuditWriter
{
    public Task WriteAsync(
        IReadOnlyList<AuditChange> changes,
        DbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException(
            "Audit writer is not configured. Register an IAuditWriter implementation or call AddEfAuditWriter<TAuditEntry>(...).");
    }
}