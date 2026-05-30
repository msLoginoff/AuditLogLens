using AuditLogLens.Changes;
using Microsoft.EntityFrameworkCore;

namespace AuditLogLens.Writing;

/// <summary>
/// Maps an <see cref="AuditChange"/> to the audit entity stored by the application.
/// </summary>
/// <typeparam name="TAuditEntry">The EF entity type used to store audit records.</typeparam>
public interface IAuditEntryMapper<out TAuditEntry>
    where TAuditEntry : class
{
    /// <summary>
    /// Determines whether this mapper can handle the supplied <see cref="DbContext"/>.
    /// </summary>
    bool CanMap(DbContext dbContext);

    /// <summary>
    /// Maps one audit change to an audit entry.
    /// </summary>
    /// <returns>
    /// The audit entry to add to the context, or <see langword="null"/> to skip the change.
    /// </returns>
    TAuditEntry? Map(AuditChange change, DbContext dbContext);
}
