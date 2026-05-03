using Microsoft.EntityFrameworkCore;

namespace AuditLogLens.Abstractions;

public interface IAuditEntryMapper<TAuditEntry>
    where TAuditEntry : class
{
    bool CanMap(DbContext dbContext);

    TAuditEntry? Map(AuditChange change, DbContext dbContext);
}