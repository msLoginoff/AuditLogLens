namespace AuditLogLens;

public class AuditRestrictionRuleBuilder
{
    private readonly HashSet<string> _forbiddenProperties;

    internal AuditRestrictionRuleBuilder(HashSet<string> forbiddenProperties)
    {
        _forbiddenProperties = forbiddenProperties;
    }

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