using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace AuditLogLens.Restrictions.Internal;

internal interface IAuditRestrictions
{
    IReadOnlyCollection<string> GetAllowedTables();

    bool IsAllowedEntry(EntityEntry entry);

    bool IsAllowedTable(string tableName);

    bool IsAllowedProperty(
        string tableName,
        string propertyName);
}