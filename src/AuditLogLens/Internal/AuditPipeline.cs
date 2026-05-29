using AuditLogLens.Detection.Internal;
using AuditLogLens.Enrichment.Internal;
using AuditLogLens.Writing.Internal;
using Microsoft.EntityFrameworkCore;

namespace AuditLogLens.Internal;

internal sealed class AuditPipeline : IAuditPipeline
{
    private readonly IAuditEnricher _enricher;
    private readonly IAuditWriter _writer;

    public AuditPipeline(
        IAuditEnricher enricher,
        IAuditWriter writer)
    {
        _enricher = enricher;
        _writer = writer;
    }

    public async Task ProcessAsync(
        DbContext dbContext,
        IReadOnlyList<AuditChange> changes,
        AuditPipelineOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(changes);

        if (changes.Count == 0)
        {
            return;
        }

        options ??= AuditPipelineOptions.Default;

        var changeList = changes as List<AuditChange> ?? changes.ToList();
        var trackedEntries = options.TrackedEntries ?? Array.Empty<AuditTrackedEntry>();

        await _enricher.EnrichAsync(
                changeList,
                dbContext,
                trackedEntries,
                cancellationToken)
            .ConfigureAwait(false);

        await _writer.WriteAsync(
                changeList,
                dbContext,
                options.SaveBehavior,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
