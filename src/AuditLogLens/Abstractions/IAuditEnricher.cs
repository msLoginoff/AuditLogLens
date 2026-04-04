using Microsoft.EntityFrameworkCore;

namespace AuditLog.Abstractions;

public interface IAuditEnricher
{
    void Enrich(List<AuditChange> changes, DbContext dbContext);
}