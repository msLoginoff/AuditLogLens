using System.Collections.Frozen;
using AuditLogLens.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace AuditLogLens;

public abstract class AuditRestrictionsBase : IAuditRestrictions
{
    private readonly Lazy<FrozenDictionary<string, FrozenSet<string>>> _rulesDictionary;

    protected AuditRestrictionsBase()
    {
        _rulesDictionary = new Lazy<FrozenDictionary<string, FrozenSet<string>>>(BuildRulesDictionary);
    }

    protected virtual void Configure(AuditRestrictionRules rules)
    {
    }

    protected virtual bool ShouldAuditEntry(EntityEntry entry) => true;

    protected virtual FrozenDictionary<string, FrozenSet<string>> BuildRulesDictionary()
    {
        var rules = new AuditRestrictionRules();
        Configure(rules);

        return rules.Build();
    }

    public virtual IReadOnlyCollection<string> GetAllowedTables()
    {
        return _rulesDictionary.Value.Keys.ToList();
    }

    public virtual bool IsAllowedEntry(EntityEntry entry)
    {
        if (entry.State is EntityState.Detached or EntityState.Unchanged)
        {
            return false;
        }

        var tableName = entry.Metadata.ClrType.Name;
        return IsAllowedTable(tableName) && ShouldAuditEntry(entry);
    }

    public virtual bool IsAllowedTable(string tableName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

        return _rulesDictionary.Value.Count == 0
               || _rulesDictionary.Value.ContainsKey(tableName);
    }

    public virtual bool IsAllowedProperty(
        string tableName,
        string propertyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        if (_rulesDictionary.Value.Count == 0)
        {
            return true;
        }

        return _rulesDictionary.Value.TryGetValue(tableName, out var forbiddenProperties)
               && !forbiddenProperties.Contains(propertyName);
    }
}