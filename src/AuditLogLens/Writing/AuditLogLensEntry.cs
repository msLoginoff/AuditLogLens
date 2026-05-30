namespace AuditLogLens.Writing;

/// <summary>
/// Default EF Core entity used by AuditLogLens when no custom audit entry is configured.
/// </summary>
public sealed class AuditLogLensEntry
{
    /// <summary>
    /// The default table name used by <c>UseAuditLogLens</c>.
    /// </summary>
    public const string DefaultTableName = "AuditLogLensEntries";

    /// <summary>
    /// Gets or sets the audit entry identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the UTC time when the audit entry was created.
    /// </summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the audited table or event group name.
    /// </summary>
    public string TableName { get; set; } = null!;

    /// <summary>
    /// Gets or sets the audit state name.
    /// </summary>
    public string State { get; set; } = null!;

    /// <summary>
    /// Gets or sets serialized values from before the change.
    /// </summary>
    public string? OldValuesJson { get; set; }

    /// <summary>
    /// Gets or sets serialized values from after the change.
    /// </summary>
    public string? NewValuesJson { get; set; }
}
