namespace AuditLogLens.Tests.TestObjects;

public sealed class RelatedEntity
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public int? NestedRelatedId { get; set; }

    public NestedRelatedEntity? NestedRelated { get; set; }
}