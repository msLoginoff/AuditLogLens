using System.Linq.Expressions;
using AuditLogLens.Enrichment.Internal.Planning;

namespace AuditLogLens.Enrichment;

public sealed class AuditReferenceOptions<TTarget>
{
    private readonly List<string> _includePaths = new();

    internal IReadOnlyList<string> IncludePaths => _includePaths;

    public AuditReferenceOptions<TTarget> Include<TProperty>(
        Expression<Func<TTarget, TProperty>> navigationPath)
    {
        ArgumentNullException.ThrowIfNull(navigationPath);

        _includePaths.Add(AuditEnrichmentExpressionHelper.GetPropertyPath(navigationPath));
        return this;
    }
}