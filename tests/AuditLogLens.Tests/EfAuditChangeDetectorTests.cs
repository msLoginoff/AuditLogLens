using AuditLogLens.Changes;
using AuditLogLens.Detection.Internal;
using AuditLogLens.Enrichment.Internal;
using AuditLogLens.Enrichment.Internal.Planning;
using AuditLogLens.Tests.TestObjects;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AuditLogLens.Tests;

public class EfAuditChangeDetectorTests
{
    private static EfAuditChangeDetector CreateDetector()
    {
        return new EfAuditChangeDetector(
            new TestAuditRestrictions(),
            new CollectionParentChangePromoter(
                new AuditEnrichmentPlanResolver(
                    new StaticDomainEnrichmentPlanProvider(),
                    new AuditEntityEnricherRegistry([]))));
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
        Assert.Equal(AuditChangeState.Added, change.State);

        Assert.True(change.NewValues.ContainsKey(nameof(AllowedEntity.Name)));
        Assert.Equal("John", change.NewValues[nameof(AllowedEntity.Name)]);

        Assert.False(change.NewValues.ContainsKey(nameof(AllowedEntity.Secret)));
        Assert.Empty(change.OldValues);

        Assert.Single(saveContext.EntriesWithTemporaryValues);
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

        Assert.Equal(AuditChangeState.Modified, change.State);

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
        Assert.Empty(saveContext.EntriesWithTemporaryValues);
    }

    [Fact]
    public void DetectPreSaveChanges_WhenOnlyForbiddenPropertyChanged_CreatesEmptyCandidate()
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

        var change = Assert.Single(saveContext.PreSaveChanges);

        Assert.Equal(AuditChangeState.Modified, change.State);
        Assert.Empty(change.OldValues);
        Assert.Empty(change.NewValues);
    }

    [Fact]
    public void DetectPreSaveChanges_WhenModifiedPropertiesHaveEqualValues_CreatesEmptyCandidate()
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
        db.Entry(entity).State = EntityState.Modified;

        var saveContext = detector.DetectPreSaveChanges(db);

        var change = Assert.Single(saveContext.PreSaveChanges);

        Assert.Equal(AuditChangeState.Modified, change.State);
        Assert.Empty(change.OldValues);
        Assert.Empty(change.NewValues);
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
        Assert.Single(saveContext.EntriesWithTemporaryValues);

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
    public void DetectPostSaveChanges_ForAddedEntityWithAuditedTemporaryForeignKey_RefreshesNewValue()
    {
        using var db = CreateDbContext();
        var detector = CreateDetector();

        var nested = new NestedRelatedEntity
        {
            Name = "Nested"
        };
        var related = new RelatedEntity
        {
            Id = 123,
            Name = "Related",
            NestedRelated = nested
        };

        db.RelatedEntities.Add(related);

        var saveContext = detector.DetectPreSaveChanges(db);

        var preSaveChange = Assert.Single(saveContext.PreSaveChanges);
        Assert.Equal(nameof(RelatedEntity), preSaveChange.EntityType.Name);
        Assert.Single(saveContext.EntriesWithTemporaryValues);
        Assert.Equal(123, preSaveChange.EntityId);

        var temporaryForeignKey = Assert.IsType<int>(
            preSaveChange.NewValues[nameof(RelatedEntity.NestedRelatedId)]);
        Assert.True(temporaryForeignKey <= 0);

        db.SaveChanges();

        var changes = detector.DetectPostSaveChanges(db, saveContext);

        var postSaveChange = Assert.Single(changes);
        var realForeignKey = Assert.IsType<int>(
            postSaveChange.NewValues[nameof(RelatedEntity.NestedRelatedId)]);

        Assert.True(realForeignKey > 0);
        Assert.Equal(nested.Id, realForeignKey);
        Assert.Equal(123, postSaveChange.EntityId);
        Assert.False(postSaveChange.IsAfterSavePhase);
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

        Assert.Equal(AuditChangeState.Deleted, change.State);

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
        Assert.Empty(saveContext.EntriesWithTemporaryValues);
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
        Assert.Empty(saveContext.EntriesWithTemporaryValues);
    }

    [Fact]
    public void DetectPreSaveChanges_WhenPolymorphicCollectionParentIsTracked_PromotesByDerivedParent()
    {
        using var db = CreateDbContext();
        var detector = CreateDetector();

        db.CollectionLookupEntities.Add(new CollectionLookupEntity
        {
            Id = 10,
            Name = "Tag"
        });
        db.PolymorphicVisitEvents.Add(new PolymorphicVisitEvent
        {
            Name = "Visit"
        });
        db.SaveChanges();

        var visitId = db.PolymorphicVisitEvents.Select(x => x.Id).Single();
        db.PolymorphicCollectionRefEntities.Add(new PolymorphicCollectionRefEntity
        {
            EventId = visitId,
            LookupId = 10
        });
        db.SaveChanges();
        db.ChangeTracker.Clear();

        var visit = db.PolymorphicVisitEvents.Single();
        var reference = db.PolymorphicCollectionRefEntities.Single();
        db.PolymorphicCollectionRefEntities.Remove(reference);

        var saveContext = detector.DetectPreSaveChanges(db);

        var change = Assert.Single(saveContext.PreSaveChanges);
        Assert.Equal(typeof(PolymorphicVisitEvent), change.EntityType);
        Assert.Equal(AuditChangeState.Modified, change.State);
        Assert.Equal(visit.Id, change.EntityId);
        Assert.Same(visit, change.Entity);
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
        Assert.Empty(saveContext.EntriesWithTemporaryValues);

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
