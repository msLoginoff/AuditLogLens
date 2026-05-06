using AuditLogLens.Enrichment;

namespace AuditLogLens.Tests.TestObjects;

public sealed class DomainConfiguredSourceEntity : IHasAuditEnrichmentConfig<DomainConfiguredSourceEntity>
{
    public int Id { get; set; }

    public int RelatedEntityId { get; set; }

    public static void ConfigureAuditEnrichment(IAuditEnrichmentPlanBuilder builder)
    {
        builder.Reference<DomainConfiguredSourceEntity, RelatedEntity, int>(
            x => x.RelatedEntityId,
            "RelatedName",
            x => x.Name);
    }
}