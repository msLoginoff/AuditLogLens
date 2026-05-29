namespace AuditLogLens;

public interface IAuditChangeFactory
{
    AuditChange CreateManual(
        string tableName,
        object? rowKey,
        AuditChangeState state,
        IReadOnlyDictionary<string, object?>? newValues = null,
        IReadOnlyDictionary<string, object?>? oldValues = null,
        object? source = null,
        Type? sourceType = null,
        IReadOnlyDictionary<string, object?>? extraValues = null);
}
