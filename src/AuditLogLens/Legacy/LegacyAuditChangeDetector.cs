using AuditLog.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace AuditLog.Legacy;

public sealed class LegacyAuditChangeDetector : IAuditChangeDetector
{
    public AuditSaveContext DetectPreSaveChanges(DbContext dbContext)
    {
        var context = new AuditSaveContext();

        // TODO:
        // 1. Перенести сюда логику из OnBeforeSaveChanges()
        // 2. Пока без записи AuditRecord и без FillAudit
        // 3. Собрать PreSaveChanges и EntriesWithTemporaryKeys

        return context;
    }

    public List<AuditChange> DetectPostSaveChanges(
        DbContext dbContext,
        AuditSaveContext saveContext)
    {
        // TODO:
        // 1. Взять saveContext.PreSaveChanges
        // 2. Дозаполнить Added-сущности, у которых появились PK после SaveChanges
        // 3. Вернуть итоговый список изменений

        return saveContext.PreSaveChanges;
    }
}