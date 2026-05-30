using System.Linq.Expressions;
using AuditLogLens.Changes;
using AuditLogLens.Enrichment.Internal.Planning;
using AuditLogLens.Enrichment.Options;
using AuditLogLens.Enrichment.Rules;

namespace AuditLogLens.Enrichment.Extensions;

/// <summary>
/// Provides preload-only lookup rules for custom enrichers.
/// </summary>
public static class LookupEnrichmentExtensions
{
    /// <summary>
    /// Declares a batched lookup that preloads entities for a custom enricher.
    /// </summary>
    /// <remarks>
    /// The library collects keys from all changes with <paramref name="keysSelector"/>,
    /// loads matching entities once, and stores them in
    /// <see cref="Context.AuditEnrichmentContext"/>. The rule does not write audit fields
    /// by itself; the enricher reads the loaded values from the context.
    /// </remarks>
    public static IAuditEnrichmentPlanBuilder Lookup<TTarget>(
        this IAuditEnrichmentPlanBuilder builder,
        string propertyName,
        Func<AuditChange, IEnumerable<object?>> keysSelector,
        Action<AuditLookupOptions<TTarget>>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        ArgumentNullException.ThrowIfNull(keysSelector);

        var options = BuildOptions(configure);

        return builder.AddRule(new LookupRule
        {
            TargetEntityType = typeof(TTarget),
            TargetPropertyName = propertyName,
            KeysSelector = keysSelector,
            IncludePaths = options.IncludePaths
        });
    }

    /// <summary>
    /// Declares a batched lookup using a typed target property expression.
    /// </summary>
    /// <remarks>
    /// This overload is equivalent to the string-based overload, but keeps the lookup
    /// property rename-safe.
    /// </remarks>
    public static IAuditEnrichmentPlanBuilder Lookup<TTarget, TKey>(
        this IAuditEnrichmentPlanBuilder builder,
        Expression<Func<TTarget, TKey>> targetProperty,
        Func<AuditChange, IEnumerable<object?>> keysSelector,
        Action<AuditLookupOptions<TTarget>>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(targetProperty);

        return builder.Lookup<TTarget>(
            ExpressionPathHelper.GetPropertyName(targetProperty),
            keysSelector,
            configure);
    }

    private static AuditLookupOptions<TTarget> BuildOptions<TTarget>(
        Action<AuditLookupOptions<TTarget>>? configure)
    {
        var options = new AuditLookupOptions<TTarget>();
        configure?.Invoke(options);
        return options;
    }
}
