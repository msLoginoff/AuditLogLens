using AuditLogLens.Detection.Internal;
using AuditLogLens.Tests.TestObjects;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AuditLogLens.Tests;

public class EfAuditChangeDetectorTests
{
    private static EfAuditChangeDetector CreateDetector()
    {
        return new EfAuditChangeDetector(new TestAuditRestrictions());
    }

    private static AuditTestDbContext CreateDbContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<AuditTestDbContext>()
            .UseSqlite(connection)
            .Options;

        var db = new AuditTestDbContext(options);
        db.Database.EnsureCreated();

        return db;
    }

    [Fact]
    public void DetectPreSaveChanges_ForAddedAllowedEntity_CreatesAuditChange()
    {
        using var db = CreateDbContext();
        var detector = CreateDetector();

        db.AllowedEntities.Add(new AllowedEntity
        {
            Name = "John",
            Secret = "hidden"
        });

        var saveContext = detector.DetectPreSaveChanges(db);

        Assert.Single(saveContext.PreSaveChanges);

        var change = saveContext.PreSaveChanges[0];

        Assert.Equal(nameof(AllowedEntity), change.TableName);
        Assert.Equal(nameof(AllowedEntity), change.EntityType.Name);
        Assert.Equal(nameof(EntityState.Added), change.State);

        Assert.True(change.NewValues.ContainsKey(nameof(AllowedEntity.Name)));
        Assert.Equal("John", change.NewValues[nameof(AllowedEntity.Name)]);

        Assert.False(change.NewValues.ContainsKey(nameof(AllowedEntity.Secret)));
        Assert.Empty(change.OldValues);

        Assert.Single(saveContext.EntriesWithTemporaryKeys);
    }

    [Fact]
    public void DetectPreSaveChanges_ForModifiedAllowedEntity_CapturesOnlyChangedAllowedProperties()
    {
        using var db = CreateDbContext();
        var detector = CreateDetector();

        db.AllowedEntities.Add(new AllowedEntity
        {
            Name = "Old",
            Secret = "s1"
        });
        db.SaveChanges();

        var entity = db.AllowedEntities.Single();
        entity.Name = "New";
        entity.Secret = "s2";

        var saveContext = detector.DetectPreSaveChanges(db);

        Assert.Single(saveContext.PreSaveChanges);

        var change = saveContext.PreSaveChanges[0];

        Assert.Equal(nameof(EntityState.Modified), change.State);

        Assert.True(change.OldValues.ContainsKey(nameof(AllowedEntity.Name)));
        Assert.True(change.NewValues.ContainsKey(nameof(AllowedEntity.Name)));
        Assert.Equal("Old", change.OldValues[nameof(AllowedEntity.Name)]);
        Assert.Equal("New", change.NewValues[nameof(AllowedEntity.Name)]);

        Assert.False(change.OldValues.ContainsKey(nameof(AllowedEntity.Secret)));
        Assert.False(change.NewValues.ContainsKey(nameof(AllowedEntity.Secret)));
    }

    [Fact]
    public void DetectPreSaveChanges_ForEntityNotInRules_SkipsIt()
    {
        using var db = CreateDbContext();
        var detector = CreateDetector();

        db.ForbiddenEntities.Add(new ForbiddenEntity
        {
            Value = "test"
        });

        var saveContext = detector.DetectPreSaveChanges(db);

        Assert.Empty(saveContext.PreSaveChanges);
        Assert.Empty(saveContext.EntriesWithTemporaryKeys);
    }

    [Fact]
    public void DetectPreSaveChanges_WhenOnlyForbiddenPropertyChanged_DoesNotCreateAuditChange()
    {
        using var db = CreateDbContext();
        var detector = CreateDetector();

        db.AllowedEntities.Add(new AllowedEntity
        {
            Name = "John",
            Secret = "s1"
        });
        db.SaveChanges();

        var entity = db.AllowedEntities.Single();
        entity.Secret = "s2";

        var saveContext = detector.DetectPreSaveChanges(db);

        Assert.Empty(saveContext.PreSaveChanges);
    }

    [Fact]
    public void DetectPreSaveChanges_ForSpecialDeleteEntityDelete_SkipsIt()
    {
        using var db = CreateDbContext();
        var detector = CreateDetector();

        db.SpecialDeleteEntities.Add(new SpecialDeleteEntity
        {
            Name = "X"
        });
        db.SaveChanges();

        var entity = db.SpecialDeleteEntities.Single();
        db.SpecialDeleteEntities.Remove(entity);

        var saveContext = detector.DetectPreSaveChanges(db);

        Assert.Empty(saveContext.PreSaveChanges);
    }

    [Fact]
    public void DetectPostSaveChanges_ForAddedEntityWithTemporaryKey_FillsRealEntityId()
    {
        using var db = CreateDbContext();
        var detector = CreateDetector();

        db.AllowedEntities.Add(new AllowedEntity
        {
            Name = "John"
        });

        var saveContext = detector.DetectPreSaveChanges(db);

        Assert.Single(saveContext.PreSaveChanges);
        Assert.Single(saveContext.EntriesWithTemporaryKeys);

        var preSaveChange = saveContext.PreSaveChanges[0];
        Assert.NotNull(preSaveChange.EntityId);

        var temporaryId = Assert.IsType<int>(preSaveChange.EntityId);
        Assert.True(temporaryId <= 0);

        db.SaveChanges();

        var changes = detector.DetectPostSaveChanges(db, saveContext);

        Assert.Single(changes);

        var postSaveChange = changes[0];
        Assert.NotNull(postSaveChange.EntityId);
        Assert.True(postSaveChange.IsAfterSavePhase);

        var realId = Assert.IsType<int>(postSaveChange.EntityId);
        Assert.True(realId > 0);
        Assert.NotEqual(temporaryId, realId);
    }

    [Fact]
    public void DetectPreSaveChanges_ForDeletedAllowedEntity_PutsValuesOnlyToOldValues()
    {
        using var db = CreateDbContext();
        var detector = CreateDetector();

        db.AllowedEntities.Add(new AllowedEntity
        {
            Name = "John",
            Secret = "hidden"
        });
        db.SaveChanges();

        var entity = db.AllowedEntities.Single();
        db.AllowedEntities.Remove(entity);

        var saveContext = detector.DetectPreSaveChanges(db);

        Assert.Single(saveContext.PreSaveChanges);

        var change = saveContext.PreSaveChanges[0];

        Assert.Equal(nameof(EntityState.Deleted), change.State);

        Assert.True(change.OldValues.ContainsKey(nameof(AllowedEntity.Name)));
        Assert.Equal("John", change.OldValues[nameof(AllowedEntity.Name)]);

        Assert.False(change.OldValues.ContainsKey(nameof(AllowedEntity.Secret)));
        Assert.Empty(change.NewValues);
    }

    [Fact]
    public void DetectPreSaveChanges_WhenAllowedAndForbiddenPropertiesChanged_TracksOnlyAllowedProperty()
    {
        using var db = CreateDbContext();
        var detector = CreateDetector();

        db.AllowedEntities.Add(new AllowedEntity
        {
            Name = "Old",
            Secret = "secret1"
        });
        db.SaveChanges();

        var entity = db.AllowedEntities.Single();
        entity.Name = "New";
        entity.Secret = "secret2";

        var saveContext = detector.DetectPreSaveChanges(db);

        Assert.Single(saveContext.PreSaveChanges);

        var change = saveContext.PreSaveChanges[0];

        Assert.True(change.OldValues.ContainsKey(nameof(AllowedEntity.Name)));
        Assert.True(change.NewValues.ContainsKey(nameof(AllowedEntity.Name)));
        Assert.Equal("Old", change.OldValues[nameof(AllowedEntity.Name)]);
        Assert.Equal("New", change.NewValues[nameof(AllowedEntity.Name)]);

        Assert.False(change.OldValues.ContainsKey(nameof(AllowedEntity.Secret)));
        Assert.False(change.NewValues.ContainsKey(nameof(AllowedEntity.Secret)));
    }

    [Fact]
    public void DetectPreSaveChanges_WhenEntityIsUnchanged_SkipsIt()
    {
        using var db = CreateDbContext();
        var detector = CreateDetector();

        db.AllowedEntities.Add(new AllowedEntity
        {
            Name = "John",
            Secret = "hidden"
        });
        db.SaveChanges();

        var entity = db.AllowedEntities.Single();

        var entry = db.Entry(entity);
        Assert.Equal(EntityState.Unchanged, entry.State);

        var saveContext = detector.DetectPreSaveChanges(db);

        Assert.Empty(saveContext.PreSaveChanges);
        Assert.Empty(saveContext.EntriesWithTemporaryKeys);
    }

    [Fact]
    public void DetectPreSaveChanges_WhenEntityIsDetached_SkipsIt()
    {
        using var db = CreateDbContext();
        var detector = CreateDetector();

        var entity = new AllowedEntity
        {
            Id = 123,
            Name = "Detached",
            Secret = "hidden"
        };

        db.Attach(entity);
        db.Entry(entity).State = EntityState.Detached;

        var saveContext = detector.DetectPreSaveChanges(db);

        Assert.Empty(saveContext.PreSaveChanges);
        Assert.Empty(saveContext.EntriesWithTemporaryKeys);
    }

    [Fact]
    public void DetectPostSaveChanges_WhenNoTemporaryKeys_ReturnsSameChangesWithoutModifications()
    {
        using var db = CreateDbContext();
        var detector = CreateDetector();

        db.AllowedEntities.Add(new AllowedEntity
        {
            Name = "Old",
            Secret = "s1"
        });
        db.SaveChanges();

        var entity = db.AllowedEntities.Single();
        entity.Name = "New";

        var saveContext = detector.DetectPreSaveChanges(db);

        Assert.Single(saveContext.PreSaveChanges);
        Assert.Empty(saveContext.EntriesWithTemporaryKeys);

        var before = saveContext.PreSaveChanges[0];
        var beforeId = before.EntityId;
        var beforeAfterSaveFlag = before.IsAfterSavePhase;

        var result = detector.DetectPostSaveChanges(db, saveContext);

        Assert.Single(result);

        var after = result[0];
        Assert.Same(before, after);
        Assert.Equal(beforeId, after.EntityId);
        Assert.Equal(beforeAfterSaveFlag, after.IsAfterSavePhase);
        Assert.False(after.IsAfterSavePhase);
    }
}