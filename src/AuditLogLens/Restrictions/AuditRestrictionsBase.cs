using System.Collections.Frozen;
using AuditLogLens.Restrictions.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace AuditLogLens.Restrictions;

/// <summary>
/// Base class for configuring which EF Core entities and properties are audited.
/// </summary>
/// <remarks>
/// AuditLogLens is opt-in. An entity is audited only when it is listed in
/// <see cref="Configure"/>. Override <see cref="ShouldAuditEntry"/> for
/// per-entry decisions that cannot be expressed as property rules.
/// </remarks>
public abstract class AuditRestrictionsBase : IAuditRestrictions
{
    private readonly Lazy<FrozenDictionary<string, FrozenSet<string>>> _rulesDictionary;

    protected AuditRestrictionsBase()
    {
        _rulesDictionary = new Lazy<FrozenDictionary<string, FrozenSet<string>>>(BuildRulesDictionary);
    }

    /// <summary>
    /// Configures the audited entities and ignored properties.
    /// </summary>
    protected virtual void Configure(AuditRestrictionRules rules)
    {
    }

    /// <summary>
    /// Determines whether a tracked entry should be audited after table restrictions have passed.
    /// </summary>
    protected virtual bool ShouldAuditEntry(EntityEntry entry) => true;

    protected virtual FrozenDictionary<string, FrozenSet<string>> BuildRulesDictionary()
    {
        var rules = new AuditRestrictionRules();
        Configure(rules);

        return rules.Build();
    }

    /// <summary>
    /// Gets the table names configured for audit detection.
    /// </summary>
    public virtual IReadOnlyCollection<string> GetAllowedTables()
    {
        return _rulesDictionary.Value.Keys.ToList();
    }

    /// <summary>
    /// Determines whether an EF Core entry can be audited.
    /// </summary>
    public virtual bool IsAllowedEntry(EntityEntry entry)
    {
        if (entry.State is EntityState.Detached or EntityState.Unchanged)
        {
            return false;
        }

        var tableName = entry.Metadata.ClrType.Name;
        return IsAllowedTable(tableName) && ShouldAuditEntry(entry);
    }

    /// <summary>
    /// Determines whether a table name is configured for audit detection.
    /// </summary>
    public virtual bool IsAllowedTable(string tableName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

        return _rulesDictionary.Value.ContainsKey(tableName);
    }

    /// <summary>
    /// Determines whether a property is allowed for an audited table.
    /// </summary>
    public virtual bool IsAllowedProperty(
        string tableName,
        string propertyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        return _rulesDictionary.Value.TryGetValue(tableName, out var forbiddenProperties)
               && !forbiddenProperties.Contains(propertyName);
    }
}
