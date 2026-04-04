namespace AuditLog.Abstractions;

public interface IAuditRestrictions
{
    IReadOnlyCollection<string> GetAllowedTables();

    bool IsAllowedTable(string tableName);

    bool IsAllowedProperty(
        string tableName,
        string propertyName,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>>? additionalRestrictions = null);
}