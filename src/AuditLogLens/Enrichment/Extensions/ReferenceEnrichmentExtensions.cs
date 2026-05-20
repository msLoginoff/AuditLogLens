using System.Linq.Expressions;
using System.Reflection;
using AuditLogLens.Enrichment.Internal.Planning;
using AuditLogLens.Enrichment.Options;
using AuditLogLens.Enrichment.Rules;

namespace AuditLogLens.Enrichment.Extensions;

public static class ReferenceEnrichmentExtensions
{
    public static IAuditEnrichmentPlanBuilder Reference<TSource, TTarget, TKey>(
        this IAuditEnrichmentPlanBuilder builder,
        Expression<Func<TSource, TKey>> foreignKeyProperty,
        string fieldName,
        Expression<Func<TTarget, object?>> valueSelector)
    {
        return builder.Reference(
            foreignKeyProperty,
            fieldName,
            valueSelector,
            configure: null);
    }

    public static IAuditEnrichmentPlanBuilder Reference<TSource, TTarget, TKey>(
        this IAuditEnrichmentPlanBuilder builder,
        Expression<Func<TSource, TKey>> foreignKeyProperty,
        string fieldName,
        Expression<Func<TTarget, object?>> valueSelector,
        Action<AuditReferenceOptions<TTarget>>? configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(foreignKeyProperty);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
        ArgumentNullException.ThrowIfNull(valueSelector);

        var options = BuildOptions(configure);

        return builder.AddRule(new ReferenceRule
        {
            TargetEntityType = typeof(TTarget),
            ForeignKeyPropertyName = ExpressionPathHelper.GetPropertyName(foreignKeyProperty),
            TargetKeyPropertyName = GetDefaultTargetKeyPropertyName<TTarget>(),
            FieldName = fieldName,
            ValueSelector = ExpressionPathHelper.CompileBoxedSelector(valueSelector),
            IncludePaths = options.IncludePaths
        });
    }

    public static IAuditEnrichmentPlanBuilder Reference<TSource, TTarget, TKey>(
        this IAuditEnrichmentPlanBuilder builder,
        Expression<Func<TSource, TKey>> foreignKeyProperty,
        Expression<Func<TTarget, TKey>> targetKeyProperty,
        string fieldName,
        Expression<Func<TTarget, object?>> valueSelector)
    {
        return builder.Reference(
            foreignKeyProperty,
            targetKeyProperty,
            fieldName,
            valueSelector,
            configure: null);
    }

    public static IAuditEnrichmentPlanBuilder Reference<TSource, TTarget, TKey>(
        this IAuditEnrichmentPlanBuilder builder,
        Expression<Func<TSource, TKey>> foreignKeyProperty,
        Expression<Func<TTarget, TKey>> targetKeyProperty,
        string fieldName,
        Expression<Func<TTarget, object?>> valueSelector,
        Action<AuditReferenceOptions<TTarget>>? configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(foreignKeyProperty);
        ArgumentNullException.ThrowIfNull(targetKeyProperty);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
        ArgumentNullException.ThrowIfNull(valueSelector);

        var options = BuildOptions(configure);

        return builder.AddRule(new ReferenceRule
        {
            TargetEntityType = typeof(TTarget),
            ForeignKeyPropertyName = ExpressionPathHelper.GetPropertyName(foreignKeyProperty),
            TargetKeyPropertyName = ExpressionPathHelper.GetPropertyName(targetKeyProperty),
            FieldName = fieldName,
            ValueSelector = ExpressionPathHelper.CompileBoxedSelector(valueSelector),
            IncludePaths = options.IncludePaths
        });
    }

    private static AuditReferenceOptions<TTarget> BuildOptions<TTarget>(
        Action<AuditReferenceOptions<TTarget>>? configure)
    {
        var options = new AuditReferenceOptions<TTarget>();
        configure?.Invoke(options);
        return options;
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