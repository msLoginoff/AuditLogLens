namespace AuditLogLens.Pipeline;

/// <summary>
/// Controls whether the audit writer only attaches audit entries to the current
/// <see cref="Microsoft.EntityFrameworkCore.DbContext"/> or also saves them immediately.
/// </summary>
public enum AuditSaveBehavior
{
    /// <summary>
    /// Adds audit entries to the current <see cref="Microsoft.EntityFrameworkCore.DbContext"/>
    /// without calling <c>SaveChanges</c>. This is the default and preferred behavior for
    /// manual audit events that should be committed together with the caller's unit of work.
    /// </summary>
    AddToCurrentContext = 0,

    /// <summary>
    /// Adds audit entries and immediately calls <c>SaveChanges</c> under AuditLogLens'
    /// audit-save suppressor. Use this only when the caller intentionally wants the
    /// current context to be saved at the same point.
    /// </summary>
    SaveImmediately = 1
}
