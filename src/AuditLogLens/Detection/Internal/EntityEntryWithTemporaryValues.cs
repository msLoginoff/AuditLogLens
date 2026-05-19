using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace AuditLogLens.Detection.Internal;

internal sealed class EntityEntryWithTemporaryValues
{
    public EntityEntryWithTemporaryValues(
        EntityEntry entry,
        bool hasTemporaryKey,
        IReadOnlySet<string> auditedTemporaryPropertyNames)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(auditedTemporaryPropertyNames);

        Entry = entry;
        HasTemporaryKey = hasTemporaryKey;
        AuditedTemporaryPropertyNames = auditedTemporaryPropertyNames;
    }

    public EntityEntry Entry { get; }

    public bool HasTemporaryKey { get; }

    public IReadOnlySet<string> AuditedTemporaryPropertyNames { get; }

    public bool HasAuditedTemporaryValues => AuditedTemporaryPropertyNames.Count > 0;
}