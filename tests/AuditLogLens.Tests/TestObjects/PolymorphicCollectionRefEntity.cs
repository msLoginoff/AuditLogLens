namespace AuditLogLens.Tests.TestObjects;

public sealed class PolymorphicCollectionRefEntity
{
    public int EventId { get; set; }

    public PolymorphicCollectionEvent? Event { get; set; }

    public int LookupId { get; set; }

    public CollectionLookupEntity? Lookup { get; set; }
}