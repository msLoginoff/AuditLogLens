using Microsoft.EntityFrameworkCore;

namespace AuditLogLens;

public interface IAuditPipeline
{
    Task ProcessAsync(
        DbContext dbContext,
        IReadOnlyList<AuditChange> changes,
        AuditPipelineOptions? options = null,
        CancellationToken cancellationToken = default);
}
