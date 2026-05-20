using AuditLogLens.Enrichment;
using AuditLogLens.Enrichment.Extensions;

namespace AuditLogLens.Tests.TestObjects;

public sealed class CollectionParentEntity : IHasAuditEnrichmentConfig<CollectionParentEntity>
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public List<CollectionRefEntity> References { get; } = [];

    public static void ConfigureAuditEnrichment(IAuditEnrichmentPlanBuilder builder)
    {
        builder.Collection<CollectionParentEntity, CollectionRefEntity, CollectionLookupEntity>(
            reference => reference.ParentId,
            reference => reference.LookupId,
            "Tags",
            lookup => lookup.Name);
    }
}