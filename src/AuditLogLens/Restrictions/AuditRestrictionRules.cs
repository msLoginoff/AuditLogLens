using System.Collections.Frozen;

namespace AuditLogLens.Restrictions;

/// <summary>
/// Collects audit restriction rules while an <see cref="AuditRestrictionsBase"/> is configured.
/// </summary>
public sealed class AuditRestrictionRules
{
    private readonly Dictionary<string, HashSet<string>> _rules = new(StringComparer.Ordinal);
    private bool _isBuilt;

    /// <summary>
    /// Marks an entity type as auditable and returns a builder for ignored properties.
    /// </summary>
    public AuditRestrictionRuleBuilder<TEntity> For<TEntity>()
    {
        return new AuditRestrictionRuleBuilder<TEntity>(GetOrAddRule(typeof(TEntity).Name));
    }

    /// <summary>
    /// Marks a table name as auditable and returns a builder for ignored properties.
    /// </summary>
    public AuditRestrictionRuleBuilder For(string tableName)
    {
        return new AuditRestrictionRuleBuilder(GetOrAddRule(tableName));
    }

    private HashSet<string> GetOrAddRule(string tableName)
    {
        if (_isBuilt)
        {
            throw new InvalidOperationException(
                $"{nameof(AuditRestrictionRules)} cannot be modified after it has been built.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

        if (!_rules.TryGetValue(tableName, out var forbiddenProperties))
        {
            forbiddenProperties = new HashSet<string>(StringComparer.Ordinal);
            _rules[tableName] = forbiddenProperties;
        }

        return forbiddenProperties;
    }

    internal FrozenDictionary<string, FrozenSet<string>> Build()
    {
        _isBuilt = true;

        return _rules.ToFrozenDictionary(
            x => x.Key,
            x => x.Value.ToFrozenSet(StringComparer.Ordinal),
            StringComparer.Ordinal);
    }
}
