using System.Linq.Expressions;
using System.Reflection;

namespace AuditLogLens;

public sealed class AuditRestrictionRuleBuilder<TEntity>
{
    private readonly HashSet<string> _forbiddenProperties;

    internal AuditRestrictionRuleBuilder(HashSet<string> forbiddenProperties)
    {
        _forbiddenProperties = forbiddenProperties;
    }

    public AuditRestrictionRuleBuilder<TEntity> Ignore(params string[] propertyNames)
    {
        ArgumentNullException.ThrowIfNull(propertyNames);

        foreach (var propertyName in propertyNames)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
            _forbiddenProperties.Add(propertyName);
        }

        return this;
    }

    public AuditRestrictionRuleBuilder<TEntity> Ignore<TProperty>(
        Expression<Func<TEntity, TProperty>> property)
    {
        Ignore(GetPropertyName(property));
        return this;
    }

    private static string GetPropertyName<TProperty>(
        Expression<Func<TEntity, TProperty>> expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        var memberExpression = UnwrapToMemberExpression(expression.Body);
        if (memberExpression.Member is not PropertyInfo propertyInfo)
        {
            throw new InvalidOperationException(
                $"Expression '{expression}' must point to a property.");
        }

        return propertyInfo.Name;
    }

    private static MemberExpression UnwrapToMemberExpression(Expression expression)
    {
        if (expression is MemberExpression memberExpression)
        {
            return memberExpression;
        }

        if (expression is UnaryExpression
            {
                NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked
            } unaryExpression)
        {
            return UnwrapToMemberExpression(unaryExpression.Operand);
        }

        throw new InvalidOperationException(
            $"Expression '{expression}' must be a simple property access like x => x.SomeProperty.");
    }
}