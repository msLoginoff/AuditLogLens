namespace AuditLogLens.Tests.TestObjects;

public sealed class CollectionRefEntity
{
    public int Id { get; set; }

    public int ParentId { get; set; }

    public CollectionParentEntity? Parent { get; set; }

    public int LookupId { get; set; }

    public CollectionLookupEntity? Lookup { get; set; }
}