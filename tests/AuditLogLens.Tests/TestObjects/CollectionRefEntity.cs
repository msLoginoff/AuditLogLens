namespace AuditLogLens.Tests.TestObjects;

public sealed class CollectionRefEntity
{
    public int Id { get; set; }

    public int ParentId { get; set; }

    public int LookupId { get; set; }
}