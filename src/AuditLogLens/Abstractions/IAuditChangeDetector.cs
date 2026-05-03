using Microsoft.EntityFrameworkCore;

namespace AuditLogLens.Abstractions;

public interface IAuditChangeDetector
{
    AuditSaveContext DetectPreSaveChanges(DbContext dbContext);

    List<AuditChange> DetectPostSaveChanges(DbContext dbContext, AuditSaveContext saveContext);
}