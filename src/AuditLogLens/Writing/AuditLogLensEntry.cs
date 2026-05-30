namespace AuditLogLens.Writing;

public sealed class AuditLogLensEntry
{
    public const string DefaultTableName = "AuditLogLensEntries";

    public Guid Id { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public string TableName { get; set; } = null!;

    public string State { get; set; } = null!;

    public string? OldValuesJson { get; set; }

    public string? NewValuesJson { get; set; }
}