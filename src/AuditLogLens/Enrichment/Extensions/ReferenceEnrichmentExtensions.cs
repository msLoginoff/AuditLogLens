using System.Linq.Expressions;
using System.Reflection;
using AuditLogLens.Enrichment.Internal.Planning;
using AuditLogLens.Enrichment.Options;
using AuditLogLens.Enrichment.Rules;

namespace AuditLogLens.Enrichment.Extensions;

/// <summary>
/// Provides fluent methods for adding reference lookups to an audit enrichment plan.
/// </summary>
public static class ReferenceEnrichmentExtensions
{
    /// <summary>
    /// Adds a reference lookup that reads a foreign key from the audited value and writes
    /// a readable value from the referenced entity.
    /// </summary>
    /// <remarks>
    /// The target entity is loaded by its public <c>Id</c> property. Use the overload with
    /// an explicit target key when the target key has a different name.
    /// </remarks>
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

    /// <summary>
    /// Adds a reference lookup and allows related data to be included when the target
    /// entity is loaded.
    /// </summary>
    /// <remarks>
    /// Include paths from all matching rules are merged and loaded in batches for the
    /// whole audit operation.
    /// </remarks>
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

    /// <summary>
    /// Adds a reference lookup with an explicit target key property.
    /// </summary>
    /// <remarks>
    /// Use this overload when the referenced entity is not keyed by a public <c>Id</c>
    /// property, or when a different lookup property should be used.
    /// </remarks>
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

    /// <summary>
    /// Adds a reference lookup with an explicit target key property and optional include paths.
    /// </summary>
    /// <remarks>
    /// The referenced values are loaded in batches before enrichers write to the audit bag.
    /// </remarks>
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
