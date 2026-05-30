using AuditLogLens.Detection;
using Microsoft.EntityFrameworkCore;

namespace AuditLogLens.Configuration;

/// <summary>
/// Configures how AuditLogLens writes audit records for automatic EF Core changes.
/// </summary>
public sealed class AuditOptions
{
    /// <summary>
    /// Gets or sets the default write mode used by the SaveChanges interceptor.
    /// </summary>
    public AuditWriteMode WriteMode { get; set; } = AuditWriteMode.NonTransactional;

    /// <summary>
    /// Gets or sets a selector that can choose the write mode for a single save operation.
    /// </summary>
    /// <remarks>
    /// When this delegate is set, its result is used instead of <see cref="WriteMode"/>.
    /// </remarks>
    public Func<DbContext, AuditSaveContext, AuditWriteMode>? WriteModeSelector { get; set; }

    internal AuditWriteMode ResolveWriteMode(DbContext dbContext, AuditSaveContext saveContext)
    {
        return WriteModeSelector?.Invoke(dbContext, saveContext) ?? WriteMode;
    }
}
