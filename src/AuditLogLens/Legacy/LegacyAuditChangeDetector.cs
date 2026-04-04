using AuditLog.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace AuditLog.Legacy;

public sealed class LegacyAuditChangeDetector : IAuditChangeDetector
{
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

    private static bool ShouldProcessEntry(EntityEntry entry)
    {
        if (entry.State is EntityState.Detached or EntityState.Unchanged)
        {
            return false;
        }

        var entityType = entry.Entity.GetType();

        //todo перенести точные условия из UnicornDbContext
        if (entityType.Name is "AuditRecord" or "ExternalAuditRecord")
        {
            return false;
        }

        return true;
    }

    private static AuditChange CreateAuditChange(EntityEntry entry, bool isAfterSave)
    {
        var auditChange = new AuditChange
        {
            Entry = entry,
            EntityType = entry.Entity.GetType(),
            EntityId = TryGetPrimaryKeyValue(entry),
            State = entry.State.ToString(),
            TableName = entry.Entity.GetType().Name,
            IsAfterSavePhase = isAfterSave
        };

        foreach (var property in entry.Properties)
        {
            if (property.Metadata.IsPrimaryKey())
            {
                continue;
            }

            var propertyName = property.Metadata.Name;

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

        return auditChange;
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
}