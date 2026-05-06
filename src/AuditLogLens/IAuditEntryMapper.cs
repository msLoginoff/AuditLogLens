using Microsoft.EntityFrameworkCore;

namespace AuditLogLens;

public interface IAuditEntryMapper<TAuditEntry>
    where TAuditEntry : class
{
    bool CanMap(DbContext dbContext);

    TAuditEntry? Map(AuditChange change, DbContext dbContext);
}