using Microsoft.EntityFrameworkCore;

namespace AuditLogLens;

/// <summary>
/// Runs the source-agnostic audit processing stage for already detected or
/// manually constructed <see cref="AuditChange"/> instances.
/// </summary>
/// <remarks>
/// The pipeline does not inspect EF Core's change tracker and does not decide
/// which domain changes should be audited. Source-specific components, such as
/// the EF Core SaveChanges interceptor or application code that creates manual
/// audit events, are responsible for building the <see cref="AuditChange"/>
/// objects. The pipeline then applies enrichment and writes audit entries.
/// </remarks>
public interface IAuditPipeline
{
    /// <summary>
    /// Enriches and writes the supplied audit changes.
    /// </summary>
    /// <param name="dbContext">
    /// The EF Core context used for enrichment lookups and audit entry writing.
    /// </param>
    /// <param name="changes">
    /// Already detected or manually constructed audit changes. The pipeline
    /// processes only this collection.
    /// </param>
    /// <param name="pipelineSettings">
    /// Optional per-call write settings. When omitted, audit entries are added
    /// to the current context without saving it.
    /// </param>
    /// <param name="cancellationToken">A token for cancelling asynchronous work.</param>
    Task ProcessAsync(
        DbContext dbContext,
        IReadOnlyList<AuditChange> changes,
        AuditPipelineSettings? pipelineSettings = null,
        CancellationToken cancellationToken = default);
}
