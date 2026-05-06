using System.Linq.Expressions;
using System.Reflection;

namespace AuditLogLens.Enrichment.Internal.Planning;

internal static class AuditEnrichmentExpressionHelper
{
    public static string GetPropertyName<TEntity, TProperty>(
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

    public static Func<object, object?> BoxValueSelector<TEntity, TValue>(
        Expression<Func<TEntity, TValue>> expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        var compiled = expression.Compile();

        return entity => compiled((TEntity)entity);
    }

    private static MemberExpression UnwrapToMemberExpression(Expression expression)
    {
        if (expression is MemberExpression memberExpression)
        {
            return memberExpression;
        }

        if (expression is UnaryExpression unaryExpression
            && unaryExpression.NodeType is ExpressionType.Convert or ExpressionType.ConvertChecked)
        {
            return UnwrapToMemberExpression(unaryExpression.Operand);
        }

        throw new InvalidOperationException(
            $"Expression '{expression}' must be a simple property access like x => x.SomeProperty.");
    }
}