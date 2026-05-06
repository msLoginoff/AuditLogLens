using AuditLogLens.Detection;
using Microsoft.EntityFrameworkCore;

namespace AuditLogLens;

public sealed class AuditOptions
{
    public AuditWriteMode WriteMode { get; set; } = AuditWriteMode.NonTransactional;

    public Func<DbContext, AuditSaveContext, AuditWriteMode>? WriteModeSelector { get; set; }

    internal AuditWriteMode ResolveWriteMode(DbContext dbContext, AuditSaveContext saveContext)
    {
        return WriteModeSelector?.Invoke(dbContext, saveContext) ?? WriteMode;
    }
}