using AuditLogLens.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace AuditLogLens.Legacy;

public sealed class LegacyAuditEnricher : IAuditEnricher
{
    public Task EnrichAsync(List<AuditChange> changes, DbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        // TODO:
        // Перенести сюда старую логику FillExtendedLogs / FillSubtenantInfoLog / Values enrichment.
        // На первом этапе можно сделать минимум для пайплайна
        return Task.CompletedTask;
    }
}