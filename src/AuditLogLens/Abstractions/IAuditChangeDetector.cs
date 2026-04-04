using Microsoft.EntityFrameworkCore;

namespace AuditLog.Abstractions;

public interface IAuditChangeDetector
{
    AuditSaveContext DetectPreSaveChanges(DbContext dbContext);

    List<AuditChange> DetectPostSaveChanges(
        DbContext dbContext,
        AuditSaveContext saveContext);
}