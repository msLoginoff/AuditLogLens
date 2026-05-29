using AuditLogLens.Detection.Internal;

namespace AuditLogLens;

/// <summary>
/// Per-call settings for <see cref="IAuditPipeline.ProcessAsync"/>.
/// </summary>
/// <remarks>
/// It describes how a single pipeline invocation should write the audit entries it receives.
/// </remarks>
public sealed class AuditPipelineSettings
{
    internal static AuditPipelineSettings Default { get; } = new();

    /// <summary>
    /// Gets the save behavior for audit entries produced by this pipeline call.
    /// The default is <see cref="AuditSaveBehavior.AddToCurrentContext"/>.
    /// </summary>
    public AuditSaveBehavior SaveBehavior { get; init; } = AuditSaveBehavior.AddToCurrentContext;

    internal IReadOnlyList<AuditTrackedEntry>? TrackedEntries { get; init; }
}
