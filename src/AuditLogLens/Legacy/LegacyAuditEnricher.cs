using AuditLog.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace AuditLog.Legacy;

public sealed class LegacyAuditEnricher : IAuditEnricher
{
    public void Enrich(List<AuditChange> changes, DbContext dbContext)
    {
        // TODO:
        // Перенести сюда старую логику FillExtendedLogs / FillSubtenantInfoLog / Values enrichment.
        // На первом этапе можно сделать минимум для пайплайна
    }
}