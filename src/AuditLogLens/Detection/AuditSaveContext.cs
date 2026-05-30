using AuditLogLens.Changes;
using AuditLogLens.Configuration;
using AuditLogLens.Detection.Internal;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;

namespace AuditLogLens.Detection;

public sealed class AuditSaveContext
{
    private readonly List<AuditTrackedEntry> _trackedEntries = new();

    public List<AuditChange> PreSaveChanges { get; } = new();

    internal List<EntityEntryWithTemporaryValues> EntriesWithTemporaryValues { get; } = new();

    internal IReadOnlyList<AuditTrackedEntry> TrackedEntries => _trackedEntries;

    public AuditWriteMode WriteMode { get; internal set; } = AuditWriteMode.NonTransactional;

    internal IDbContextTransaction? Transaction { get; set; }

    internal bool OwnsTransaction { get; set; }

    internal void CaptureTrackedEntries(IEnumerable<EntityEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        _trackedEntries.Clear();
        _trackedEntries.AddRange(entries.Select(entry => new AuditTrackedEntry(entry)));
    }
}