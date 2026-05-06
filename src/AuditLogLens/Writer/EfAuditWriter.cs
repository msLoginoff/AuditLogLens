using AuditLogLens.Abstractions;
using AuditLogLens.Interceptors;
using Microsoft.EntityFrameworkCore;

namespace AuditLogLens.Writer;

public sealed class EfAuditWriter<TAuditEntry> : IAuditWriter
    where TAuditEntry : class
{
    private readonly IEnumerable<IAuditEntryMapper<TAuditEntry>> _mappers;
    private readonly AuditSaveChangesSuppressor _suppressor;

    public EfAuditWriter(
        IEnumerable<IAuditEntryMapper<TAuditEntry>> mappers,
        AuditSaveChangesSuppressor suppressor)
    {
        _mappers = mappers;
        _suppressor = suppressor;
    }

    public async Task WriteAsync(
        IReadOnlyList<AuditChange> changes,
        DbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(changes);
        ArgumentNullException.ThrowIfNull(dbContext);

        if (changes.Count == 0)
            return;

        var mapper = _mappers.LastOrDefault(x => x.CanMap(dbContext))
                     ?? throw new InvalidOperationException(
                         $"No audit entry mapper for {typeof(TAuditEntry).FullName} can map {dbContext.GetType().FullName}.");

        if (dbContext.Model.FindEntityType(typeof(TAuditEntry)) is null)
        {
            throw new InvalidOperationException(
                $"Audit entry type {typeof(TAuditEntry).FullName} is not part of the EF model for {dbContext.GetType().FullName}. " +
                "Add it to the DbContext model or configure another audit writer.");
        }

        var auditEntries = changes
            .Select(change => mapper.Map(change, dbContext))
            .OfType<TAuditEntry>()
            .ToList();

        if (auditEntries.Count == 0)
            return;

        dbContext.Set<TAuditEntry>().AddRange(auditEntries);

        using (_suppressor.Suppress())
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}