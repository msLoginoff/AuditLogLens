using System.Linq.Expressions;
using AuditLogLens.Enrichment.Internal.Planning;
using AuditLogLens.Enrichment.Options;
using AuditLogLens.Enrichment.Rules;

namespace AuditLogLens.Enrichment.Extensions;

public static class LookupEnrichmentExtensions
{
    /// <summary>
    /// Declares a preload-only batched lookup. The library collects keys from
    /// all changes using <paramref name="keysSelector"/>, runs a single
    /// <c>WHERE propertyName IN (...)</c> load (with optional <c>Include</c> paths
    /// merged with other rules for the same target + property), and stores the
    /// loaded entities in <see cref="Context.AuditEnrichmentContext"/>. The
    /// enricher consumes them via <c>context.GetLoaded&lt;TTarget&gt;(propertyName)</c>.
    /// </summary>
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
    /// Same as <see cref="Lookup{TTarget}(IAuditEnrichmentPlanBuilder,string,Func{AuditChange,IEnumerable{object?}},Action{AuditLookupOptions{TTarget}})"/>
    /// but accepts a typed property expression for rename-safety.
    /// </summary>
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