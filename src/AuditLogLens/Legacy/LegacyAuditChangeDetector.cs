using AuditLog.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace AuditLog.Legacy;

public sealed class LegacyAuditChangeDetector : IAuditChangeDetector
{
    private readonly IAuditRestrictions _auditRestrictions;

    public LegacyAuditChangeDetector(IAuditRestrictions auditRestrictions)
    {
        _auditRestrictions = auditRestrictions;
    }

    public AuditSaveContext DetectPreSaveChanges(DbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        dbContext.ChangeTracker.DetectChanges();

        var saveContext = new AuditSaveContext();

        foreach (var entry in dbContext.ChangeTracker.Entries().ToList())
        {
            if (!ShouldProcessEntry(entry))
            {
                continue;
            }

            var auditChange = CreateAuditChange(entry, isAfterSave: false);

            if (auditChange is null)
            {
                continue;
            }

            if (HasTemporaryKey(entry))
            {
                saveContext.EntriesWithTemporaryKeys.Add(entry);
            }

            saveContext.PreSaveChanges.Add(auditChange);
        }

        return saveContext;
    }

    public List<AuditChange> DetectPostSaveChanges(
        DbContext dbContext,
        AuditSaveContext saveContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(saveContext);

        var result = saveContext.PreSaveChanges;

        if (saveContext.EntriesWithTemporaryKeys.Count == 0)
        {
            return result;
        }

        foreach (var entry in saveContext.EntriesWithTemporaryKeys)
        {
            var existingChange = result.FirstOrDefault(x => ReferenceEquals(x.Entry, entry));
            if (existingChange is null)
            {
                continue;
            }

            existingChange.EntityId = TryGetPrimaryKeyValue(entry);
            existingChange.IsAfterSavePhase = true;
        }

        return result;
    }

    private bool ShouldProcessEntry(EntityEntry entry)
    {
        return _auditRestrictions.IsAllowedEntry(entry);
    }

    private AuditChange? CreateAuditChange(EntityEntry entry, bool isAfterSave)
    {
        var tableName = GetAuditTableName(entry);
        if (string.IsNullOrWhiteSpace(tableName))
        {
            return null;
        }

        var auditChange = new AuditChange
        {
            Entry = entry,
            EntityType = entry.Metadata.ClrType,
            EntityId = TryGetPrimaryKeyValue(entry),
            State = entry.State.ToString(),
            TableName = tableName,
            IsAfterSavePhase = isAfterSave
        };

        foreach (var property in entry.Properties)
        {
            if (property.Metadata.IsPrimaryKey())
            {
                continue;
            }

            var propertyName = property.Metadata.Name;

            if (!_auditRestrictions.IsAllowedProperty(tableName, propertyName))
            {
                continue;
            }

            switch (entry.State)
            {
                case EntityState.Added:
                    auditChange.NewValues[propertyName] = property.CurrentValue;
                    break;

                case EntityState.Deleted:
                    auditChange.OldValues[propertyName] = property.OriginalValue;
                    break;

                case EntityState.Modified:
                    if (!property.IsModified)
                    {
                        continue;
                    }

                    auditChange.OldValues[propertyName] = property.OriginalValue;
                    auditChange.NewValues[propertyName] = property.CurrentValue;
                    break;
            }
        }

        if (!HasVisibleChanges(auditChange) && !HasTemporaryKey(entry))
        {
            return null;
        }

        return auditChange;
    }

    private static bool HasVisibleChanges(AuditChange change)
    {
        return change.OldValues.Count > 0 || change.NewValues.Count > 0;
    }

    private static bool HasTemporaryKey(EntityEntry entry)
    {
        return entry.Properties.Any(p => p.Metadata.IsPrimaryKey() && p.IsTemporary);
    }

    private static object? TryGetPrimaryKeyValue(EntityEntry entry)
    {
        var key = entry.Metadata.FindPrimaryKey();
        if (key is null || key.Properties.Count == 0)
        {
            return null;
        }

        if (key.Properties.Count == 1)
        {
            return entry.Property(key.Properties[0].Name).CurrentValue;
        }

        var values = new Dictionary<string, object?>();
        foreach (var property in key.Properties)
        {
            values[property.Name] = entry.Property(property.Name).CurrentValue;
        }

        return values;
    }

    private static string? GetAuditTableName(EntityEntry entry)
    {
        return entry.Metadata.ClrType.Name;
    }
}