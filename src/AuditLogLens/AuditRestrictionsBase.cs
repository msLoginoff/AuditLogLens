using AuditLog.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace AuditLog;

public abstract class AuditRestrictionsBase : IAuditRestrictions
{
    private readonly Lazy<IReadOnlyDictionary<string, IReadOnlyCollection<string>>> _rulesDictionary;

    protected AuditRestrictionsBase()
    {
        _rulesDictionary = new Lazy<IReadOnlyDictionary<string, IReadOnlyCollection<string>>>(BuildRulesDictionary);
    }

    protected abstract IReadOnlyCollection<AuditRestrictionRule> Rules { get; }

    protected virtual IReadOnlyDictionary<string, IReadOnlyCollection<string>> BuildRulesDictionary()
    {
        return Rules.ToDictionary(
            x => x.AllowedTable,
            x => x.ForbiddenProperties);
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
        return IsAllowedTable(tableName);
    }

    public virtual bool IsAllowedTable(string tableName)
    {
        return _rulesDictionary.Value.ContainsKey(tableName);
    }

    public virtual bool IsAllowedProperty(
        string tableName,
        string propertyName,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>>? additionalRestrictions = null)
    {
        if (additionalRestrictions is not null
            && additionalRestrictions.TryGetValue(tableName, out var additionallyForbidden)
            && additionallyForbidden.Contains(propertyName))
        {
            return false;
        }

        return _rulesDictionary.Value.TryGetValue(tableName, out var forbiddenProperties)
               && !forbiddenProperties.Contains(propertyName);
    }
}