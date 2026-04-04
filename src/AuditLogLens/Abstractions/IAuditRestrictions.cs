using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace AuditLog.Abstractions;

public interface IAuditRestrictions
{
    IReadOnlyCollection<string> GetAllowedTables();

    bool IsAllowedEntry(EntityEntry entry);

    bool IsAllowedTable(string tableName);

    bool IsAllowedProperty(
        string tableName,
        string propertyName,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>>? additionalRestrictions = null);
}