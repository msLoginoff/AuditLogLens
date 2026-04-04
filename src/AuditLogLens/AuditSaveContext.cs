using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace AuditLog;

public sealed class AuditSaveContext
{
    public List<AuditChange> PreSaveChanges { get; } = new();

    public List<EntityEntry> EntriesWithTemporaryKeys { get; } = new();
}