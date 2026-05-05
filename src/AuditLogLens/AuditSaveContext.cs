using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;

namespace AuditLogLens;

public sealed class AuditSaveContext
{
    public List<AuditChange> PreSaveChanges { get; } = new();

    public List<EntityEntry> EntriesWithTemporaryKeys { get; } = new();

    public AuditWriteMode WriteMode { get; internal set; } = AuditWriteMode.NonTransactional;

    internal IDbContextTransaction? Transaction { get; set; }

    internal bool OwnsTransaction { get; set; }
}