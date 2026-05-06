using Microsoft.EntityFrameworkCore;

namespace AuditLogLens.Detection.Internal;

internal interface IAuditChangeDetector
{
    AuditSaveContext DetectPreSaveChanges(DbContext dbContext);

    List<AuditChange> DetectPostSaveChanges(DbContext dbContext, AuditSaveContext saveContext);
}