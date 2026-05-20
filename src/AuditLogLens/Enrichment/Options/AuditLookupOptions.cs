using System.Linq.Expressions;
using AuditLogLens.Enrichment.Internal.Planning;

namespace AuditLogLens.Enrichment.Options;

public sealed class AuditLookupOptions<TTarget>
{
    private readonly List<string> _includePaths = new();

    internal IReadOnlyList<string> IncludePaths => _includePaths;

    public AuditLookupOptions<TTarget> Include<TProperty>(
        Expression<Func<TTarget, TProperty>> navigationPath)
    {
        ArgumentNullException.ThrowIfNull(navigationPath);

        _includePaths.Add(ExpressionPathHelper.GetPropertyPath(navigationPath));
        return this;
    }
}