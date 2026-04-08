using Microsoft.EntityFrameworkCore;

namespace AuditLog.Abstractions;

public interface IAuditEntryMapper<TAuditEntry>
{
    bool CanMap(DbContext dbContext);
    TAuditEntry Map(AuditChange change, DbContext dbContext);
}