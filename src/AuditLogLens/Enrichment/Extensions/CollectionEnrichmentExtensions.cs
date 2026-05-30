using System.Linq.Expressions;
using System.Reflection;
using AuditLogLens.Enrichment.Internal.Planning;
using AuditLogLens.Enrichment.Rules;

namespace AuditLogLens.Enrichment.Extensions;

/// <summary>
/// Provides fluent methods for enriching audit records from explicit collection join entities.
/// </summary>
public static class CollectionEnrichmentExtensions
{
    /// <summary>
    /// Adds enrichment for explicit many-to-many or one-to-many collection changes represented by a join entity.
    /// </summary>
    /// <remarks>
    /// EF Core implicit skip-navigation many-to-many relationships without a CLR join entity
    /// are not supported. The parent and item entities are expected to have public
    /// <c>Id</c> properties.
    /// </remarks>
    public static IAuditEnrichmentPlanBuilder Collection<TSource, TJoin, TItem>(
        this IAuditEnrichmentPlanBuilder builder,
        Expression<Func<TJoin, object?>> joinParentKey,
        Expression<Func<TJoin, object?>> joinItemKey,
        string fieldName,
        Expression<Func<TItem, object?>> itemValueSelector)
        where TSource : class
        where TJoin : class
        where TItem : class
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(joinParentKey);
        ArgumentNullException.ThrowIfNull(joinItemKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
        ArgumentNullException.ThrowIfNull(itemValueSelector);

        return builder.AddRule(new CollectionRule
        {
            ParentEntityType = typeof(TSource),
            JoinEntityType = typeof(TJoin),
            ItemEntityType = typeof(TItem),
            ParentKeyPropertyName = GetDefaultKeyPropertyName<TSource>(),
            JoinParentKeyPropertyName = ExpressionPathHelper.GetPropertyName(joinParentKey),
            JoinItemKeyPropertyName = ExpressionPathHelper.GetPropertyName(joinItemKey),
            ItemKeyPropertyName = GetDefaultKeyPropertyName<TItem>(),
            FieldName = fieldName,
            ItemValueSelector = ExpressionPathHelper.CompileBoxedSelector(itemValueSelector)
        });
    }

    /// <summary>
    /// Adds enrichment for collection changes represented by a join entity and explicit key properties.
    /// </summary>
    /// <remarks>
    /// This overload should be used when the parent or item key is not named <c>Id</c>.
    /// Collection enrichment needs tracked join-entry snapshots from the EF interceptor path;
    /// manual audit changes can only use it when those snapshots are supplied by the caller.
    /// </remarks>
    public static IAuditEnrichmentPlanBuilder Collection<TSource, TJoin, TItem, TParentKey, TItemKey>(
        this IAuditEnrichmentPlanBuilder builder,
        Expression<Func<TSource, TParentKey>> parentKey,
        Expression<Func<TJoin, TParentKey>> joinParentKey,
        Expression<Func<TJoin, TItemKey>> joinItemKey,
        Expression<Func<TItem, TItemKey>> itemKey,
        string fieldName,
        Expression<Func<TItem, object?>> itemValueSelector)
        where TSource : class
        where TJoin : class
        where TItem : class
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
        ArgumentNullException.ThrowIfNull(parentKey);
        ArgumentNullException.ThrowIfNull(joinParentKey);
        ArgumentNullException.ThrowIfNull(joinItemKey);
        ArgumentNullException.ThrowIfNull(itemKey);
        ArgumentNullException.ThrowIfNull(itemValueSelector);

        return builder.AddRule(new CollectionRule
        {
            ParentEntityType = typeof(TSource),
            JoinEntityType = typeof(TJoin),
            ItemEntityType = typeof(TItem),
            ParentKeyPropertyName = ExpressionPathHelper.GetPropertyName(parentKey),
            JoinParentKeyPropertyName = ExpressionPathHelper.GetPropertyName(joinParentKey),
            JoinItemKeyPropertyName = ExpressionPathHelper.GetPropertyName(joinItemKey),
            ItemKeyPropertyName = ExpressionPathHelper.GetPropertyName(itemKey),
            FieldName = fieldName,
            ItemValueSelector = ExpressionPathHelper.CompileBoxedSelector(itemValueSelector)
        });
    }

    private static string GetDefaultKeyPropertyName<TEntity>()
    {
        var property = typeof(TEntity).GetProperty(
            "Id",
            BindingFlags.Public | BindingFlags.Instance);

        if (property is not null)
        {
            return property.Name;
        }

        throw new InvalidOperationException(
            $"Type {typeof(TEntity).FullName} does not have a public instance property named 'Id'. " +
            "Use the overload with explicit parentKey and itemKey.");
    }
}
