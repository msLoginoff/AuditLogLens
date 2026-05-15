using System.Linq.Expressions;
using AuditLogLens.Enrichment.Internal.Planning;
using AuditLogLens.Enrichment.Rules;

namespace AuditLogLens.Enrichment;

public static class AuditEnrichmentPlanBuilderCollectionExtensions
{
    /// <summary>
    /// Adds enrichment for explicit many-to-many or one-to-many collection changes represented by a join entity.
    /// EF Core implicit skip-navigation many-to-many relationships without a CLR join entity are not supported yet.
    /// </summary>
    public static IAuditEnrichmentPlanBuilder Collection<TSource, TJoin, TItem, TParentKey, TItemKey>(
        this IAuditEnrichmentPlanBuilder builder,
        string fieldName,
        Expression<Func<TSource, TParentKey>> parentKey,
        Expression<Func<TJoin, TParentKey>> joinParentKey,
        Expression<Func<TJoin, TItemKey>> joinItemKey,
        Expression<Func<TItem, TItemKey>> itemKey,
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
}