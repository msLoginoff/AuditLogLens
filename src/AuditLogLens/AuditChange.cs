using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace AuditLogLens;

public sealed class AuditChange
{
    public required Type EntityType { get; init; }

    public object? EntityId { get; set; }

    public required string State { get; init; }

    public string? TableName { get; init; }

    public int? TenantId { get; set; }

    public int? SubtenantId { get; set; }

    public int? PatientId { get; set; }

    public string? UserId { get; set; }

    public Dictionary<string, object?> OldValues { get; } = new();

    public Dictionary<string, object?> NewValues { get; } = new();

    public bool IsAfterSavePhase { get; set; }

    public EntityEntry? Entry { get; init; }
}