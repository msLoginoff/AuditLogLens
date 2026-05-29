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
    /// Optional original object behind the manual audit event. The factory stores it
    /// in <see cref="AuditChange.Entity"/> and, unless <paramref name="sourceType"/>
    /// is provided, uses <c>source.GetType()</c> as <see cref="AuditChange.EntityType"/>.
    /// Use this when enrichers need the actual object, not just the value dictionaries.
    /// </param>
    /// <param name="sourceType">
    /// Optional type used only to select enrichment rules by setting
    /// <see cref="AuditChange.EntityType"/>. This is useful when the manual event has
    /// no source object, but should still use enrichment configured for a payload type.
    /// When both <paramref name="source"/> and <paramref name="sourceType"/> are provided,
    /// <paramref name="sourceType"/> wins for rule resolution while
    /// <paramref name="source"/> remains available through <see cref="AuditChange.Entity"/>.
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
