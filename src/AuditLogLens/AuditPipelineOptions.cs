using AuditLogLens.Detection.Internal;

namespace AuditLogLens;

public sealed class AuditPipelineOptions
{
    internal static AuditPipelineOptions Default { get; } = new();

    public AuditSaveBehavior SaveBehavior { get; init; } = AuditSaveBehavior.AddToCurrentContext;

    internal IReadOnlyList<AuditTrackedEntry>? TrackedEntries { get; init; }
}
