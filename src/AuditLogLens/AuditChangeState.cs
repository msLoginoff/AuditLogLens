namespace AuditLogLens;

/// <summary>
/// Describes the logical state of an audit change.
/// </summary>
public enum AuditChangeState
{
    /// <summary>
    /// The audited subject was created. Values usually belong in
    /// <see cref="AuditChange.NewValues"/>.
    /// </summary>
    Added = 0,

    /// <summary>
    /// The audited subject was changed. Values usually belong in both
    /// <see cref="AuditChange.OldValues"/> and <see cref="AuditChange.NewValues"/>.
    /// </summary>
    Modified = 1,

    /// <summary>
    /// The audited subject was removed. Values usually belong in
    /// <see cref="AuditChange.OldValues"/>.
    /// </summary>
    Deleted = 2
}
