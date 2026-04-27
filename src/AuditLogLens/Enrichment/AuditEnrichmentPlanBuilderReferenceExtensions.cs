using System.Linq.Expressions;
using System.Reflection;

namespace AuditLog.Enrichment;

public static class AuditEnrichmentPlanBuilderReferenceExtensions
{
    public static IAuditEnrichmentPlanBuilder Reference<TSource, TTarget, TKey>(
        this IAuditEnrichmentPlanBuilder builder,
        Expression<Func<TSource, TKey>> foreignKeyProperty,
        string fieldName,
        Expression<Func<TTarget, object?>> valueSelector)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(foreignKeyProperty);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
        ArgumentNullException.ThrowIfNull(valueSelector);

        return builder.AddRule(new ReferenceRule
        {
            TargetEntityType = typeof(TTarget),
            ForeignKeyPropertyName = AuditEnrichmentExpressionHelper.GetPropertyName(foreignKeyProperty),
            TargetKeyPropertyName = GetDefaultTargetKeyPropertyName<TTarget>(),
            FieldName = fieldName,
            ValueSelector = AuditEnrichmentExpressionHelper.BoxValueSelector(valueSelector)
        });
    }

    public static IAuditEnrichmentPlanBuilder Reference<TSource, TTarget, TKey>(
        this IAuditEnrichmentPlanBuilder builder,
        Expression<Func<TSource, TKey>> foreignKeyProperty,
        Expression<Func<TTarget, TKey>> targetKeyProperty,
        string fieldName,
        Expression<Func<TTarget, object?>> valueSelector)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(foreignKeyProperty);
        ArgumentNullException.ThrowIfNull(targetKeyProperty);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
        ArgumentNullException.ThrowIfNull(valueSelector);

        return builder.AddRule(new ReferenceRule
        {
            TargetEntityType = typeof(TTarget),
            ForeignKeyPropertyName = AuditEnrichmentExpressionHelper.GetPropertyName(foreignKeyProperty),
            TargetKeyPropertyName = AuditEnrichmentExpressionHelper.GetPropertyName(targetKeyProperty),
            FieldName = fieldName,
            ValueSelector = AuditEnrichmentExpressionHelper.BoxValueSelector(valueSelector)
        });
    }

    private static string GetDefaultTargetKeyPropertyName<TTarget>()
    {
        var property = typeof(TTarget).GetProperty(
            "Id",
            BindingFlags.Public | BindingFlags.Instance);

        if (property is null)
        {
            throw new InvalidOperationException(
                $"Type {typeof(TTarget).FullName} does not have a public instance property named 'Id'. " +
                "Use the overload with explicit targetKeyProperty.");
        }

        return property.Name;
    }
}