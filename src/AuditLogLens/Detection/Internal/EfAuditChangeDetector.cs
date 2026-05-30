using AuditLogLens.Changes;
using AuditLogLens.Restrictions.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace AuditLogLens.Detection.Internal;

internal sealed class EfAuditChangeDetector : IAuditChangeDetector
{
    private readonly IAuditRestrictions _auditRestrictions;
    private readonly CollectionParentChangePromoter _collectionParentChangePromoter;

    public EfAuditChangeDetector(
        IAuditRestrictions auditRestrictions,
        CollectionParentChangePromoter collectionParentChangePromoter)
    {
        _auditRestrictions = auditRestrictions;
        _collectionParentChangePromoter = collectionParentChangePromoter;
    }

    public AuditSaveContext DetectPreSaveChanges(DbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        dbContext.ChangeTracker.DetectChanges();

        var saveContext = new AuditSaveContext();
        var entries = dbContext.ChangeTracker
            .Entries()
            .Where(entry => entry.State != EntityState.Detached)
            .ToList();

        saveContext.CaptureTrackedEntries(entries);

        foreach (var entry in entries.Where(ShouldProcessEntry))
        {
            var auditChange = CreateAuditChange(entry, isAfterSave: false);

            if (auditChange is null)
            {
                continue;
            }

            var temporaryValues = GetTemporaryValues(entry, auditChange);
            if (temporaryValues.HasTemporaryKey || temporaryValues.HasAuditedTemporaryValues)
            {
                saveContext.EntriesWithTemporaryValues.Add(temporaryValues);
            }

            saveContext.PreSaveChanges.Add(auditChange);
        }

        _collectionParentChangePromoter.Promote(
            dbContext,
            saveContext,
            rule => _auditRestrictions.IsAllowedTable(rule.ParentEntityType.Name));

        return saveContext;
    }

    public List<AuditChange> DetectPostSaveChanges(
        DbContext dbContext,
        AuditSaveContext saveContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(saveContext);

        var result = saveContext.PreSaveChanges;

        if (saveContext.EntriesWithTemporaryValues.Count == 0)
        {
            return result;
        }

        foreach (var temporaryValues in saveContext.EntriesWithTemporaryValues)
        {
            var entry = temporaryValues.Entry;
            var existingChange = result.FirstOrDefault(x => ReferenceEquals(x.Entry, entry));
            if (existingChange is null)
            {
                continue;
            }

            if (temporaryValues.HasTemporaryKey)
            {
                existingChange.EntityId = TryGetPrimaryKeyValue(entry);
                existingChange.IsAfterSavePhase = true;
            }

            if (temporaryValues.HasAuditedTemporaryValues)
            {
                RefreshAddedTemporaryNewValues(
                    existingChange,
                    entry,
                    temporaryValues.AuditedTemporaryPropertyNames);
            }
        }

        return result;
    }

    // For Added entries, properties snapshotted at PreSave may have held temporary FK
    // values when the referenced entity was also Added in the same save and only got
    // its real key during the actual INSERT. EF's relationship fixup writes the real
    // value onto the tracked entry post-save — re-snapshot here so NewValues reflects
    // it. We only refresh keys already present (restrictions filtering happened at
    // PreSave; we update existing values without changing the audited key set).
    private static void RefreshAddedTemporaryNewValues(
        AuditChange change,
        EntityEntry entry,
        IReadOnlySet<string> temporaryPropertyNames)
    {
        if (change.State != AuditChangeState.Added)
        {
            return;
        }

        foreach (var property in entry.Properties)
        {
            if (property.Metadata.IsPrimaryKey())
            {
                continue;
            }

            var propertyName = property.Metadata.Name;
            if (!temporaryPropertyNames.Contains(propertyName))
            {
                continue;
            }

            if (!change.NewValues.ContainsKey(propertyName))
            {
                continue;
            }

            change.NewValues[propertyName] = property.CurrentValue;
        }
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
            Entity = entry.Entity,
            EntityType = entry.Metadata.ClrType,
            EntityId = TryGetPrimaryKeyValue(entry),
            State = ToAuditChangeState(entry.State),
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

                    if (Equals(property.OriginalValue, property.CurrentValue))
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

    private static EntityEntryWithTemporaryValues GetTemporaryValues(
        EntityEntry entry,
        AuditChange change)
    {
        var hasTemporaryKey = false;
        var auditedTemporaryPropertyNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var property in entry.Properties)
        {
            if (!property.IsTemporary)
            {
                continue;
            }

            if (property.Metadata.IsPrimaryKey())
            {
                hasTemporaryKey = true;
            }

            if (change.State == AuditChangeState.Added
                && change.NewValues.ContainsKey(property.Metadata.Name))
            {
                auditedTemporaryPropertyNames.Add(property.Metadata.Name);
            }
        }

        return new EntityEntryWithTemporaryValues(
            entry,
            hasTemporaryKey,
            auditedTemporaryPropertyNames);
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

    private static AuditChangeState ToAuditChangeState(EntityState state)
    {
        return state switch
        {
            EntityState.Added => AuditChangeState.Added,
            EntityState.Modified => AuditChangeState.Modified,
            EntityState.Deleted => AuditChangeState.Deleted,
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
        };
    }
}
