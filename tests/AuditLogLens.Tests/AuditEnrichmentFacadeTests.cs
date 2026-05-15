using AuditLogLens.Detection.Internal;
using AuditLogLens.Enrichment;
using AuditLogLens.Enrichment.Internal;
using AuditLogLens.Enrichment.Internal.Planning;
using AuditLogLens.Enrichment.Rules;
using AuditLogLens.Tests.TestObjects;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AuditLogLens.Tests;

public class AuditEnrichmentFacadeTests
{
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
    public async Task EnrichAsync_LoadsReferenceDataGloballyAcrossSourceTypes()
    {
        await using var db = CreateDbContext();

        db.RelatedEntities.AddRange(
            new RelatedEntity { Id = 1, Name = "First" },
            new RelatedEntity { Id = 2, Name = "Second" });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var firstChange = new AuditChange
        {
            EntityType = typeof(FirstSourceEntity),
            State = nameof(EntityState.Added)
        };
        firstChange.NewValues[nameof(FirstSourceEntity.RelatedEntityId)] = 1;

        var secondChange = new AuditChange
        {
            EntityType = typeof(SecondSourceEntity),
            State = nameof(EntityState.Added)
        };
        secondChange.NewValues[nameof(SecondSourceEntity.RelatedEntityId)] = 2;

        var enricher = new AuditEnrichmentFacade(
            new TestDomainEnrichmentPlanProvider(),
            new AuditEntityEnricherRegistry([]));

        await enricher.EnrichAsync(
            [firstChange, secondChange],
            db,
            TestContext.Current.CancellationToken);

        Assert.Equal("First", firstChange.NewValues["RelatedName"]);
        Assert.Equal("Second", secondChange.NewValues["RelatedName"]);
    }

    [Fact]
    public async Task EnrichAsync_DoesNotApplyReferenceRuleForModifiedChangeWhenForeignKeyWasNotChanged()
    {
        await using var db = CreateDbContext();

        db.RelatedEntities.Add(new RelatedEntity { Id = 1, Name = "Readable" });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var source = new FirstSourceEntity
        {
            Id = 100,
            RelatedEntityId = 1
        };

        db.Attach(source);

        var change = new AuditChange
        {
            EntityType = typeof(FirstSourceEntity),
            State = nameof(EntityState.Modified),
            Entry = db.Entry(source)
        };
        change.OldValues["Name"] = "Old";
        change.NewValues["Name"] = "New";

        var enricher = new AuditEnrichmentFacade(
            new TestDomainEnrichmentPlanProvider(),
            new AuditEntityEnricherRegistry([]));

        await enricher.EnrichAsync(
            [change],
            db,
            TestContext.Current.CancellationToken);

        Assert.False(change.OldValues.ContainsKey("RelatedName"));
        Assert.False(change.NewValues.ContainsKey("RelatedName"));
    }

    [Fact]
    public async Task EnrichAsync_AppliesReferenceRuleForModifiedChangeWhenForeignKeyWasChanged()
    {
        await using var db = CreateDbContext();

        db.RelatedEntities.AddRange(
            new RelatedEntity { Id = 1, Name = "Old Readable" },
            new RelatedEntity { Id = 2, Name = "New Readable" });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var source = new FirstSourceEntity
        {
            Id = 100,
            RelatedEntityId = 2
        };

        db.Attach(source);

        var change = new AuditChange
        {
            EntityType = typeof(FirstSourceEntity),
            State = nameof(EntityState.Modified),
            Entry = db.Entry(source)
        };
        change.OldValues[nameof(FirstSourceEntity.RelatedEntityId)] = 1;
        change.NewValues[nameof(FirstSourceEntity.RelatedEntityId)] = 2;

        var enricher = new AuditEnrichmentFacade(
            new TestDomainEnrichmentPlanProvider(),
            new AuditEntityEnricherRegistry([]));

        await enricher.EnrichAsync(
            [change],
            db,
            TestContext.Current.CancellationToken);

        Assert.Equal("Old Readable", change.OldValues["RelatedName"]);
        Assert.Equal("New Readable", change.NewValues["RelatedName"]);
    }

    [Fact]
    public async Task EnrichAsync_AppliesCollectionRuleForTrackedRefChanges()
    {
        await using var db = CreateDbContext();

        db.CollectionParentEntities.Add(new CollectionParentEntity { Id = 1, Name = "Parent" });
        db.CollectionLookupEntities.AddRange(
            new CollectionLookupEntity { Id = 10, Name = "Old Tag" },
            new CollectionLookupEntity { Id = 20, Name = "New Tag" });
        db.CollectionRefEntities.Add(new CollectionRefEntity
        {
            Id = 100,
            ParentId = 1,
            LookupId = 10
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var parent = db.CollectionParentEntities.Single();
        parent.Name = "Changed";

        var oldRef = db.CollectionRefEntities.Single();
        db.CollectionRefEntities.Remove(oldRef);
        db.CollectionRefEntities.Add(new CollectionRefEntity
        {
            ParentId = 1,
            LookupId = 20
        });

        var change = new AuditChange
        {
            EntityType = typeof(CollectionParentEntity),
            EntityId = parent.Id,
            State = nameof(EntityState.Modified),
            Entry = db.Entry(parent)
        };
        change.OldValues[nameof(CollectionParentEntity.Name)] = "Parent";
        change.NewValues[nameof(CollectionParentEntity.Name)] = "Changed";

        var trackedEntries = db.ChangeTracker
            .Entries()
            .Where(entry => entry.State != EntityState.Detached)
            .Select(entry => new AuditTrackedEntry(entry))
            .ToList();

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var enricher = new AuditEnrichmentFacade(
            new TestDomainEnrichmentPlanProvider(),
            new AuditEntityEnricherRegistry([]));

        await enricher.EnrichAsync(
            [change],
            db,
            trackedEntries,
            TestContext.Current.CancellationToken);

        var oldTags = Assert.IsAssignableFrom<IEnumerable<object?>>(change.OldValues["Tags"]);
        var newTags = Assert.IsAssignableFrom<IEnumerable<object?>>(change.NewValues["Tags"]);

        Assert.Equal(["Old Tag"], oldTags);
        Assert.Equal(["New Tag"], newTags);
    }

    private sealed class TestDomainEnrichmentPlanProvider : IAuditDomainEnrichmentPlanProvider
    {
        public AuditEnrichmentPlan GetPlan(Type entityType)
        {
            if (entityType == typeof(FirstSourceEntity))
            {
                return BuildReferencePlan(nameof(FirstSourceEntity.RelatedEntityId));
            }

            if (entityType == typeof(SecondSourceEntity))
            {
                return BuildReferencePlan(nameof(SecondSourceEntity.RelatedEntityId));
            }

            if (entityType == typeof(CollectionParentEntity))
            {
                var builder = new AuditEnrichmentPlanBuilder();
                builder.Collection<CollectionParentEntity, CollectionRefEntity, CollectionLookupEntity, int, int>(
                    "Tags",
                    parent => parent.Id,
                    reference => reference.ParentId,
                    reference => reference.LookupId,
                    lookup => lookup.Id,
                    lookup => lookup.Name);

                return new AuditEnrichmentPlan(
                    rules: builder.Build().Rules,
                    customSteps: []);
            }

            return AuditEnrichmentPlan.Empty;
        }

        private static AuditEnrichmentPlan BuildReferencePlan(string foreignKeyPropertyName)
        {
            return new AuditEnrichmentPlan(
                rules:
                [
                    new ReferenceRule
                    {
                        TargetEntityType = typeof(RelatedEntity),
                        ForeignKeyPropertyName = foreignKeyPropertyName,
                        TargetKeyPropertyName = nameof(RelatedEntity.Id),
                        FieldName = "RelatedName",
                        ValueSelector = entity => ((RelatedEntity)entity).Name
                    }
                ],
                customSteps: []);
        }
    }
}