using AuditLogLens.Changes;
using AuditLogLens.Configuration;
using AuditLogLens.Detection.Internal;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;

namespace AuditLogLens.Detection;

/// <summary>
/// Holds audit state collected during a single EF Core SaveChanges operation.
/// </summary>
/// <remarks>
/// Application code normally does not create this type. It is exposed so
/// <see cref="Configuration.AuditOptions.WriteModeSelector"/> can inspect the detected
/// changes before choosing how audit entries should be written.
/// </remarks>
public sealed class AuditSaveContext
{
    private readonly List<AuditTrackedEntry> _trackedEntries = new();

    /// <summary>
    /// Gets the audit changes detected before EF Core saves the domain changes.
    /// </summary>
    public List<AuditChange> PreSaveChanges { get; } = new();

    internal List<EntityEntryWithTemporaryValues> EntriesWithTemporaryValues { get; } = new();

    internal IReadOnlyList<AuditTrackedEntry> TrackedEntries => _trackedEntries;

    /// <summary>
    /// Gets the write mode selected for this SaveChanges operation.
    /// </summary>
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
