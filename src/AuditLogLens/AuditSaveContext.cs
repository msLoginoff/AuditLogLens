using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace AuditLogLens;

public sealed class AuditSaveContext
{
    public List<AuditChange> PreSaveChanges { get; } = new();

    public List<EntityEntry> EntriesWithTemporaryKeys { get; } = new();
}