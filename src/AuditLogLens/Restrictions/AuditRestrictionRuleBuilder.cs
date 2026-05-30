namespace AuditLogLens.Restrictions;

/// <summary>
/// Builds property restrictions for a table configured by name.
/// </summary>
public class AuditRestrictionRuleBuilder
{
    private readonly HashSet<string> _forbiddenProperties;

    internal AuditRestrictionRuleBuilder(HashSet<string> forbiddenProperties)
    {
        _forbiddenProperties = forbiddenProperties;
    }

    /// <summary>
    /// Excludes properties from audit values for the configured table.
    /// </summary>
    public AuditRestrictionRuleBuilder Ignore(params string[] propertyNames)
    {
        ArgumentNullException.ThrowIfNull(propertyNames);

        foreach (var propertyName in propertyNames)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
            _forbiddenProperties.Add(propertyName);
        }

        return this;
    }
}
