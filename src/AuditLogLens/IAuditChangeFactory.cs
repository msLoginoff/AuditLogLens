namespace AuditLogLens;

/// <summary>
/// Creates <see cref="AuditChange"/> instances for audit events that do not
/// originate from EF Core's change tracker.
/// </summary>
/// <remarks>
/// The factory intentionally accepts already prepared value dictionaries. It
/// does not reflect over payload objects, serialize arbitrary DTOs, apply audit
/// restrictions, or calculate diffs. Those decisions belong to the application
/// code that owns the manual audit event.
/// </remarks>
public interface IAuditChangeFactory
{
    /// <summary>
    /// Creates a manual audit change from explicit old/new/extra value dictionaries.
    /// </summary>
    /// <param name="tableName">The logical audited table or event group name.</param>
    /// <param name="rowKey">
    /// The logical row key or event key. It is stored in <see cref="AuditChange.EntityId"/>
    /// so application mappers can map it to their audit record row-key field.
    /// </param>
    /// <param name="state">The logical audit state for the manual event.</param>
    /// <param name="newValues">Values to place in <see cref="AuditChange.NewValues"/>.</param>
    /// <param name="oldValues">Values to place in <see cref="AuditChange.OldValues"/>.</param>
    /// <param name="source">
    /// Optional source object for enrichment rules that need typed source data.
    /// </param>
    /// <param name="sourceType">
    /// Optional source type to use when <paramref name="source"/> is null or when
    /// the caller wants enrichment to resolve rules for a different type.
    /// </param>
    /// <param name="extraValues">
    /// Additional metadata values available to audit entry mappers and enrichers.
    /// </param>
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
