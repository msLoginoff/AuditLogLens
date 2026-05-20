using System.Linq.Expressions;
using System.Reflection;

namespace AuditLogLens.Enrichment.Internal.Planning;

internal static class ExpressionPathHelper
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

    public static string GetPropertyPath<TEntity, TProperty>(
        Expression<Func<TEntity, TProperty>> expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        var members = new Stack<string>();
        var current = UnwrapConvert(expression.Body);

        while (current is MemberExpression memberExpression)
        {
            if (memberExpression.Member is not PropertyInfo)
            {
                throw new InvalidOperationException(
                    $"Expression '{expression}' must point to a property path.");
            }

            members.Push(memberExpression.Member.Name);
            current = memberExpression.Expression is null
                ? null
                : UnwrapConvert(memberExpression.Expression);
        }

        if (current is not ParameterExpression || members.Count == 0)
        {
            throw new InvalidOperationException(
                $"Expression '{expression}' must be a property path like x => x.SomeNavigation.");
        }

        return string.Join(".", members);
    }

    public static Func<object, object?> CompileBoxedSelector<TEntity, TValue>(
        Expression<Func<TEntity, TValue>> expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        var compiled = expression.Compile();

        return entity => compiled((TEntity)entity);
    }

    private static MemberExpression UnwrapToMemberExpression(Expression expression)
    {
        expression = UnwrapConvert(expression);

        if (expression is MemberExpression memberExpression)
        {
            return memberExpression;
        }

        throw new InvalidOperationException(
            $"Expression '{expression}' must be a simple property access like x => x.SomeProperty.");
    }

    private static Expression UnwrapConvert(Expression expression)
    {
        while (expression is UnaryExpression
               {
                   NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked
               } unaryExpression)
        {
            expression = unaryExpression.Operand;
        }

        return expression;
    }
}