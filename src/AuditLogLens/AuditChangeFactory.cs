namespace AuditLogLens;

/// <summary>
/// Default implementation of <see cref="IAuditChangeFactory"/>.
/// </summary>
public sealed class AuditChangeFactory : IAuditChangeFactory
{
    /// <inheritdoc />
    public AuditChange CreateManual(
        string tableName,
        object? rowKey,
        AuditChangeState state,
        IReadOnlyDictionary<string, object?>? newValues = null,
        IReadOnlyDictionary<string, object?>? oldValues = null,
        object? source = null,
        Type? sourceType = null,
        IReadOnlyDictionary<string, object?>? extraValues = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

        var change = new AuditChange
        {
            EntityType = sourceType ?? source?.GetType() ?? typeof(object),
            Entity = source,
            EntityId = rowKey,
            State = state,
            TableName = tableName
        };

        CopyValues(oldValues, change.OldValues);
        CopyValues(newValues, change.NewValues);
        CopyExtraValues(extraValues, change);

        return change;
    }

    private static void CopyValues(
        IReadOnlyDictionary<string, object?>? values,
        Dictionary<string, object?> target)
    {
        if (values is null)
        {
            return;
        }

        foreach (var (key, value) in values)
        {
            target[key] = value;
        }
    }

    private static void CopyExtraValues(
        IReadOnlyDictionary<string, object?>? values,
        AuditChange change)
    {
        if (values is null)
        {
            return;
        }

        foreach (var (key, value) in values)
        {
            change.SetExtraValue(key, value);
        }
    }

}
