namespace AuditLogLens.Tests.TestObjects;

public sealed class TestAuditEntry
{
    public int Id { get; set; }

    public string? TableName { get; set; }

    public string? EntityId { get; set; }

    public string? State { get; set; }

    public string? NewName { get; set; }

    public string? NewTags { get; set; }
}