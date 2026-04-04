using AuditLog.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace AuditLog.Legacy;

public sealed class LegacyEfAuditWriter : IAuditWriter
{
    public Task WriteAsync(
        List<AuditChange> changes,
        DbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        // TODO:
        // 1. Преобразовать AuditChange -> AuditRecord
        // 2. Добавить в DbSet<AuditRecord>
        // 3. Аккуратно сохранить, не вызывая рекурсивно аудит!!!!

        return Task.CompletedTask;
    }
}