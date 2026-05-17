using System.Linq.Expressions;
using System.Reflection;
using AuditLogLens.Enrichment.Internal.Planning;
using AuditLogLens.Enrichment.Rules;

namespace AuditLogLens.Enrichment;

public static class AuditEnrichmentPlanBuilderCollectionExtensions
{
    /// <summary>
    /// Adds enrichment for explicit many-to-many or one-to-many collection changes represented by a join entity.
    /// EF Core implicit skip-navigation many-to-many relationships without a CLR join entity are not supported yet.
    /// </summary>
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
            JoinParentKeyPropertyName = AuditEnrichmentExpressionHelper.GetPropertyName(joinParentKey),
            JoinItemKeyPropertyName = AuditEnrichmentExpressionHelper.GetPropertyName(joinItemKey),
            ItemKeyPropertyName = GetDefaultKeyPropertyName<TItem>(),
            FieldName = fieldName,
            ItemValueSelector = AuditEnrichmentExpressionHelper.BoxValueSelector(itemValueSelector)
        });
    }

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
            ParentKeyPropertyName = AuditEnrichmentExpressionHelper.GetPropertyName(parentKey),
            JoinParentKeyPropertyName = AuditEnrichmentExpressionHelper.GetPropertyName(joinParentKey),
            JoinItemKeyPropertyName = AuditEnrichmentExpressionHelper.GetPropertyName(joinItemKey),
            ItemKeyPropertyName = AuditEnrichmentExpressionHelper.GetPropertyName(itemKey),
            FieldName = fieldName,
            ItemValueSelector = AuditEnrichmentExpressionHelper.BoxValueSelector(itemValueSelector)
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