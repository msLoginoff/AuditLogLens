using AuditLogLens.Changes;
using Microsoft.EntityFrameworkCore;

namespace AuditLogLens.Writing;

public interface IAuditEntryMapper<out TAuditEntry>
    where TAuditEntry : class
{
    bool CanMap(DbContext dbContext);

    TAuditEntry? Map(AuditChange change, DbContext dbContext);
}