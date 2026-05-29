using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace AuditLogLens.Writing.Internal;

internal sealed class DefaultAuditLogLensEntryMapper : IAuditEntryMapper<AuditLogLensEntry>
{
    public bool CanMap(DbContext dbContext) => true;

    public AuditLogLensEntry Map(AuditChange change, DbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(change);
        ArgumentNullException.ThrowIfNull(dbContext);

        return new AuditLogLensEntry
        {
            Id = Guid.NewGuid(),
            CreatedAtUtc = DateTime.UtcNow,
            TableName = change.TableName ?? change.EntityType.Name,
            State = change.State.ToString(),
            OldValuesJson = SerializeDictionaryOrNull(change.OldValues),
            NewValuesJson = SerializeDictionaryOrNull(change.NewValues)
        };
    }

    private static string? SerializeDictionaryOrNull(
        IReadOnlyDictionary<string, object?> values)
    {
        return values.Count == 0
            ? null
            : JsonSerializer.Serialize(values);
    }
}
