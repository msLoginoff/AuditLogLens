using AuditLogLens.Enrichment;
using AuditLogLens.Enrichment.Extensions;

namespace AuditLogLens.Tests.TestObjects;

public abstract class PolymorphicCollectionEvent
{
    public int Id { get; set; }
}

public sealed class PolymorphicVisitEvent :
    PolymorphicCollectionEvent,
    IHasAuditEnrichmentConfig<PolymorphicVisitEvent>
{
    public string? Name { get; set; }

    public static void ConfigureAuditEnrichment(IAuditEnrichmentPlanBuilder builder)
    {
        builder.Collection<PolymorphicVisitEvent, PolymorphicCollectionRefEntity, CollectionLookupEntity>(
            reference => reference.EventId,
            reference => reference.LookupId,
            "Tags",
            lookup => lookup.Name);
    }
}

public sealed class PolymorphicAbsenceEvent : PolymorphicCollectionEvent
{
    public string? Reason { get; set; }
}