using System.Linq.Expressions;
using AuditLogLens.Enrichment.Internal.Planning;

namespace AuditLogLens.Enrichment.Options;

/// <summary>
/// Configures reference enrichment for a target entity type.
/// </summary>
/// <typeparam name="TTarget">The entity type loaded by the reference rule.</typeparam>
public sealed class AuditReferenceOptions<TTarget>
{
    private readonly List<string> _includePaths = new();

    internal IReadOnlyList<string> IncludePaths => _includePaths;

    /// <summary>
    /// Includes a navigation path when loading referenced entities.
    /// </summary>
    public AuditReferenceOptions<TTarget> Include<TProperty>(
        Expression<Func<TTarget, TProperty>> navigationPath)
    {
        ArgumentNullException.ThrowIfNull(navigationPath);

        _includePaths.Add(ExpressionPathHelper.GetPropertyPath(navigationPath));
        return this;
    }
}
