using System.Data.Common;
using AuditLogLens.Changes;
using AuditLogLens.Detection.Internal;
using AuditLogLens.Enrichment;
using AuditLogLens.Enrichment.Context;
using AuditLogLens.Enrichment.Extensions;
using AuditLogLens.Enrichment.Internal;
using AuditLogLens.Enrichment.Internal.Planning;
using AuditLogLens.Enrichment.Rules;
using AuditLogLens.Tests.TestObjects;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
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

    private static AuditTestDbContext CreateDbContext(params IInterceptor[] interceptors)
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var optionsBuilder = new DbContextOptionsBuilder<AuditTestDbContext>()
            .UseSqlite(connection);

        if (interceptors.Length > 0)
        {
            optionsBuilder.AddInterceptors(interceptors);
        }

        var db = new AuditTestDbContext(optionsBuilder.Options);
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
            State = AuditChangeState.Added
        };
        firstChange.NewValues[nameof(FirstSourceEntity.RelatedEntityId)] = 1;

        var secondChange = new AuditChange
        {
            EntityType = typeof(SecondSourceEntity),
            State = AuditChangeState.Added
        };
        secondChange.NewValues[nameof(SecondSourceEntity.RelatedEntityId)] = 2;

        var enricher = new AuditEnrichmentFacade(
            CreatePlanResolver(),
            new AuditEntityEnricherRegistry([]));

        await enricher.EnrichAsync(
            [firstChange, secondChange],
            db,
            CaptureTrackedEntries(db),
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
            State = AuditChangeState.Modified,
            Entry = db.Entry(source)
        };
        change.OldValues["Name"] = "Old";
        change.NewValues["Name"] = "New";

        var enricher = new AuditEnrichmentFacade(
            CreatePlanResolver(),
            new AuditEntityEnricherRegistry([]));

        await enricher.EnrichAsync(
            [change],
            db,
            CaptureTrackedEntries(db),
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
            State = AuditChangeState.Modified,
            Entry = db.Entry(source)
        };
        change.OldValues[nameof(FirstSourceEntity.RelatedEntityId)] = 1;
        change.NewValues[nameof(FirstSourceEntity.RelatedEntityId)] = 2;

        var enricher = new AuditEnrichmentFacade(
            CreatePlanResolver(),
            new AuditEntityEnricherRegistry([]));

        await enricher.EnrichAsync(
            [change],
            db,
            CaptureTrackedEntries(db),
            TestContext.Current.CancellationToken);

        Assert.Equal("Old Readable", change.OldValues["RelatedName"]);
        Assert.Equal("New Readable", change.NewValues["RelatedName"]);
    }

    [Fact]
    public async Task EnrichAsync_ReferenceRuleLoadsConfiguredIncludesForNestedProjection()
    {
        await using var db = CreateDbContext();

        db.NestedRelatedEntities.Add(new NestedRelatedEntity { Id = 10, Name = "Nested readable" });
        db.RelatedEntities.Add(new RelatedEntity
        {
            Id = 1,
            Name = "Related",
            NestedRelatedId = 10
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.ChangeTracker.Clear();

        var change = new AuditChange
        {
            EntityType = typeof(FirstSourceEntity),
            State = AuditChangeState.Added
        };
        change.NewValues[nameof(FirstSourceEntity.RelatedEntityId)] = 1;

        var builder = new AuditEnrichmentPlanBuilder();
        builder.Reference<FirstSourceEntity, RelatedEntity, int>(
            source => source.RelatedEntityId,
            "NestedRelatedName",
            related => related.NestedRelated == null ? null : related.NestedRelated.Name,
            options => options.Include(related => related.NestedRelated));

        var enricher = new AuditEnrichmentFacade(
            CreatePlanResolver(builder.Build()),
            new AuditEntityEnricherRegistry([]));

        await enricher.EnrichAsync(
            [change],
            db,
            CaptureTrackedEntries(db),
            TestContext.Current.CancellationToken);

        Assert.Equal("Nested readable", change.NewValues["NestedRelatedName"]);
    }

    [Fact]
    public async Task EnrichAsync_BatchesReferenceRulesWithIncludesForSameTargetAndKey()
    {
        var commandCounter = new DbCommandCounterInterceptor();
        await using var db = CreateDbContext(commandCounter);

        db.NestedRelatedEntities.Add(new NestedRelatedEntity { Id = 10, Name = "Nested readable" });
        db.RelatedEntities.Add(new RelatedEntity
        {
            Id = 1,
            Name = "Related readable",
            NestedRelatedId = 10
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.ChangeTracker.Clear();
        commandCounter.Reset();

        var change = new AuditChange
        {
            EntityType = typeof(FirstSourceEntity),
            State = AuditChangeState.Added
        };
        change.NewValues[nameof(FirstSourceEntity.RelatedEntityId)] = 1;

        var builder = new AuditEnrichmentPlanBuilder();
        builder
            .Reference<FirstSourceEntity, RelatedEntity, int>(
                source => source.RelatedEntityId,
                "RelatedName",
                related => related.Name)
            .Reference<FirstSourceEntity, RelatedEntity, int>(
                source => source.RelatedEntityId,
                "NestedRelatedName",
                related => related.NestedRelated == null ? null : related.NestedRelated.Name,
                options => options.Include(related => related.NestedRelated));

        var enricher = new AuditEnrichmentFacade(
            CreatePlanResolver(builder.Build()),
            new AuditEntityEnricherRegistry([]));

        await enricher.EnrichAsync(
            [change],
            db,
            CaptureTrackedEntries(db),
            TestContext.Current.CancellationToken);

        Assert.Equal("Related readable", change.NewValues["RelatedName"]);
        Assert.Equal("Nested readable", change.NewValues["NestedRelatedName"]);
        Assert.Equal(1, commandCounter.ReaderCommands);
    }

    [Fact]
    public async Task EnrichAsync_RunsAllEnricherBeforeMergeHooksBeforeMergingBags()
    {
        await using var db = CreateDbContext();

        var change = new AuditChange
        {
            EntityType = typeof(FirstSourceEntity),
            State = AuditChangeState.Modified
        };

        var enricher = new AuditEnrichmentFacade(
            CreatePlanResolver(AuditEnrichmentPlan.Empty),
            new AuditEntityEnricherRegistry([
                new FirstBeforeMergeEnricher(),
                new SecondBeforeMergeEnricher(),
                new AfterMergeEnricher()
            ]));

        await enricher.EnrichAsync(
            [change],
            db,
            CaptureTrackedEntries(db),
            TestContext.Current.CancellationToken);

        Assert.Equal("first", change.NewValues["First"]);
        Assert.Equal("second", change.NewValues["Second"]);
        Assert.Equal("after", change.NewValues["After"]);
    }

    [Fact]
    public async Task EnrichAsync_RunsPerChangeHooksForHandledChangesOnly()
    {
        await using var db = CreateDbContext();

        var firstChange = new AuditChange
        {
            EntityType = typeof(FirstSourceEntity),
            State = AuditChangeState.Modified
        };
        var secondChange = new AuditChange
        {
            EntityType = typeof(SecondSourceEntity),
            State = AuditChangeState.Modified
        };

        var enricher = new AuditEnrichmentFacade(
            CreatePlanResolver(AuditEnrichmentPlan.Empty),
            new AuditEntityEnricherRegistry([new PerChangeEnricher()]));

        await enricher.EnrichAsync(
            [firstChange, secondChange],
            db,
            CaptureTrackedEntries(db),
            TestContext.Current.CancellationToken);

        Assert.Equal("before", firstChange.NewValues["PerChangeBefore"]);
        Assert.Equal("after", firstChange.NewValues["PerChangeAfter"]);
        Assert.False(secondChange.NewValues.ContainsKey("PerChangeBefore"));
        Assert.False(secondChange.NewValues.ContainsKey("PerChangeAfter"));
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
            State = AuditChangeState.Modified,
            Entry = db.Entry(parent)
        };
        change.OldValues[nameof(CollectionParentEntity.Name)] = "Parent";
        change.NewValues[nameof(CollectionParentEntity.Name)] = "Changed";

        var trackedEntries = CaptureTrackedEntries(db);

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var enricher = new AuditEnrichmentFacade(
            CreatePlanResolver(),
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

    [Fact]
    public async Task EnrichAsync_AppliesCollectionRuleForAddedParent()
    {
        await using var db = CreateDbContext();

        db.CollectionLookupEntities.AddRange(
            new CollectionLookupEntity { Id = 10, Name = "First Tag" },
            new CollectionLookupEntity { Id = 20, Name = "Second Tag" });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var parent = new CollectionParentEntity { Id = 1, Name = "Parent" };
        db.CollectionParentEntities.Add(parent);
        db.CollectionRefEntities.AddRange(
            new CollectionRefEntity { ParentId = 1, LookupId = 10 },
            new CollectionRefEntity { ParentId = 1, LookupId = 20 });

        var change = CreateParentChange(parent, db.Entry(parent), AuditChangeState.Added);
        var trackedEntries = CaptureTrackedEntries(db);

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        await EnrichAsync(db, change, trackedEntries);

        Assert.False(change.OldValues.ContainsKey("Tags"));
        AssertCollectionValue(change.NewValues["Tags"], "First Tag", "Second Tag");
    }

    [Fact]
    public async Task EnrichAsync_AppliesCollectionRuleForAddedParentWithGeneratedKeyAndJoinNavigation()
    {
        await using var db = CreateDbContext();

        db.CollectionLookupEntities.Add(new CollectionLookupEntity { Id = 10, Name = "Generated Parent Tag" });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var parent = new CollectionParentEntity { Name = "Parent" };
        parent.References.Add(new CollectionRefEntity { LookupId = 10 });
        db.CollectionParentEntities.Add(parent);

        var change = CreateParentChange(parent, db.Entry(parent), AuditChangeState.Added);
        var trackedEntries = CaptureTrackedEntries(db);

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        change.EntityId = parent.Id;

        await EnrichAsync(db, change, trackedEntries);

        Assert.True(parent.Id > 0);
        Assert.False(change.OldValues.ContainsKey("Tags"));
        AssertCollectionValue(change.NewValues["Tags"], "Generated Parent Tag");
    }

    [Fact]
    public async Task EnrichAsync_CollectionRuleUsesPreSaveJoinNavigationSnapshot()
    {
        await using var db = CreateDbContext();

        db.CollectionLookupEntities.Add(new CollectionLookupEntity { Id = 10, Name = "Snapshot Tag" });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var firstParent = new CollectionParentEntity { Name = "First Parent" };
        var secondParent = new CollectionParentEntity { Name = "Second Parent" };
        firstParent.References.Add(new CollectionRefEntity { LookupId = 10 });

        db.CollectionParentEntities.AddRange(firstParent, secondParent);

        var firstChange = CreateParentChange(firstParent, db.Entry(firstParent), AuditChangeState.Added);
        var secondChange = CreateParentChange(secondParent, db.Entry(secondParent), AuditChangeState.Added);
        var trackedEntries = CaptureTrackedEntries(db);

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        firstChange.EntityId = firstParent.Id;
        secondChange.EntityId = secondParent.Id;

        var join = db.CollectionRefEntities.Single();
        join.Parent = secondParent;

        await EnrichAsync(db, firstChange, secondChange, trackedEntries);

        AssertCollectionValue(firstChange.NewValues["Tags"], "Snapshot Tag");
        Assert.False(secondChange.NewValues.ContainsKey("Tags"));
    }

    [Fact]
    public async Task EnrichAsync_AppliesCollectionRuleForDeletedParent()
    {
        await using var db = CreateDbContext();

        db.CollectionParentEntities.Add(new CollectionParentEntity { Id = 1, Name = "Parent" });
        db.CollectionLookupEntities.Add(new CollectionLookupEntity { Id = 10, Name = "Deleted Tag" });
        db.CollectionRefEntities.Add(new CollectionRefEntity { ParentId = 1, LookupId = 10 });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var parent = db.CollectionParentEntities.Single();
        db.CollectionParentEntities.Remove(parent);

        var change = CreateParentChange(parent, db.Entry(parent), AuditChangeState.Deleted);
        var trackedEntries = CaptureTrackedEntries(db);

        await EnrichAsync(db, change, trackedEntries);

        AssertCollectionValue(change.OldValues["Tags"], "Deleted Tag");
        Assert.False(change.NewValues.ContainsKey("Tags"));
    }

    [Fact]
    public async Task EnrichAsync_AppliesCollectionRuleOnlyToMatchingParent()
    {
        await using var db = CreateDbContext();

        db.CollectionParentEntities.AddRange(
            new CollectionParentEntity { Id = 1, Name = "First" },
            new CollectionParentEntity { Id = 2, Name = "Second" });
        db.CollectionLookupEntities.AddRange(
            new CollectionLookupEntity { Id = 10, Name = "First Tag" },
            new CollectionLookupEntity { Id = 20, Name = "Second Tag" });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var first = db.CollectionParentEntities.Single(x => x.Id == 1);
        var second = db.CollectionParentEntities.Single(x => x.Id == 2);
        first.Name = "First Changed";
        second.Name = "Second Changed";

        db.CollectionRefEntities.AddRange(
            new CollectionRefEntity { ParentId = 1, LookupId = 10 },
            new CollectionRefEntity { ParentId = 2, LookupId = 20 });

        var firstChange = CreateParentChange(first, db.Entry(first), AuditChangeState.Modified);
        var secondChange = CreateParentChange(second, db.Entry(second), AuditChangeState.Modified);
        var trackedEntries = CaptureTrackedEntries(db);

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        await EnrichAsync(db, firstChange, secondChange, trackedEntries);

        AssertCollectionValue(firstChange.NewValues["Tags"], "First Tag");
        AssertCollectionValue(secondChange.NewValues["Tags"], "Second Tag");
    }

    [Fact]
    public void Collection_FullOverloadBuildsRuleWithExplicitKeys()
    {
        var builder = new AuditEnrichmentPlanBuilder();

        builder.Collection<CollectionParentEntity, CollectionRefEntity, CollectionLookupEntity, int, int>(
            parentKey: parent => parent.Id,
            joinParentKey: reference => reference.ParentId,
            joinItemKey: reference => reference.LookupId,
            itemKey: lookup => lookup.Id,
            fieldName: "Tags",
            itemValueSelector: lookup => lookup.Name);

        var rule = Assert.IsType<CollectionRule>(Assert.Single(builder.Build().Rules));

        Assert.Equal(typeof(CollectionParentEntity), rule.ParentEntityType);
        Assert.Equal(typeof(CollectionRefEntity), rule.JoinEntityType);
        Assert.Equal(typeof(CollectionLookupEntity), rule.ItemEntityType);
        Assert.Equal(nameof(CollectionParentEntity.Id), rule.ParentKeyPropertyName);
        Assert.Equal(nameof(CollectionRefEntity.ParentId), rule.JoinParentKeyPropertyName);
        Assert.Equal(nameof(CollectionRefEntity.LookupId), rule.JoinItemKeyPropertyName);
        Assert.Equal(nameof(CollectionLookupEntity.Id), rule.ItemKeyPropertyName);
        Assert.Equal("Tags", rule.FieldName);
    }

    [Fact]
    public async Task EnrichAsync_CollectionRuleCanUseEntityIdWhenParentEntryIsUnavailable()
    {
        await using var db = CreateDbContext();

        var change = new AuditChange
        {
            EntityType = typeof(CollectionParentEntity),
            EntityId = 1,
            State = AuditChangeState.Modified
        };

        await EnrichAsync(db, change, []);

        Assert.False(change.NewValues.ContainsKey("Tags"));
    }

    private sealed class TestDomainEnrichmentPlanProvider : IDomainEnrichmentPlanProvider
    {
        public AuditEnrichmentPlan GetPlanFor(Type entityType)
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
                builder.Collection<CollectionParentEntity, CollectionRefEntity, CollectionLookupEntity>(
                    reference => reference.ParentId,
                    reference => reference.LookupId,
                    "Tags",
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

    private static IReadOnlyList<AuditTrackedEntry> CaptureTrackedEntries(DbContext db)
    {
        return db.ChangeTracker
            .Entries()
            .Where(entry => entry.State != EntityState.Detached)
            .Select(entry => new AuditTrackedEntry(entry))
            .ToList();
    }

    private static AuditChange CreateParentChange(
        CollectionParentEntity parent,
        EntityEntry entry,
        AuditChangeState state)
    {
        return new AuditChange
        {
            EntityType = typeof(CollectionParentEntity),
            EntityId = parent.Id,
            State = state,
            Entry = entry
        };
    }

    private static async Task EnrichAsync(
        AuditTestDbContext db,
        AuditChange change,
        IReadOnlyList<AuditTrackedEntry> trackedEntries)
    {
        await EnrichAsync(db, [change], trackedEntries);
    }

    private static async Task EnrichAsync(
        AuditTestDbContext db,
        AuditChange firstChange,
        AuditChange secondChange,
        IReadOnlyList<AuditTrackedEntry> trackedEntries)
    {
        await EnrichAsync(db, [firstChange, secondChange], trackedEntries);
    }

    private static async Task EnrichAsync(
        AuditTestDbContext db,
        List<AuditChange> changes,
        IReadOnlyList<AuditTrackedEntry> trackedEntries)
    {
        var enricher = new AuditEnrichmentFacade(
            CreatePlanResolver(),
            new AuditEntityEnricherRegistry([]));

        await enricher.EnrichAsync(
            changes,
            db,
            trackedEntries,
            TestContext.Current.CancellationToken);
    }

    private static AuditEnrichmentPlanResolver CreatePlanResolver()
    {
        return new AuditEnrichmentPlanResolver(
            new TestDomainEnrichmentPlanProvider(),
            new AuditEntityEnricherRegistry([]));
    }

    private static AuditEnrichmentPlanResolver CreatePlanResolver(AuditEnrichmentPlan plan)
    {
        return new AuditEnrichmentPlanResolver(
            new StaticPlanProvider(plan),
            new AuditEntityEnricherRegistry([]));
    }

    private static void AssertCollectionValue(object? actual, params string[] expected)
    {
        var values = Assert.IsAssignableFrom<IEnumerable<object?>>(actual);
        Assert.Equal(expected, values);
    }

    private sealed class StaticPlanProvider : IDomainEnrichmentPlanProvider
    {
        private readonly AuditEnrichmentPlan _plan;

        public StaticPlanProvider(AuditEnrichmentPlan plan)
        {
            _plan = plan;
        }

        public AuditEnrichmentPlan GetPlanFor(Type entityType)
        {
            return entityType == typeof(FirstSourceEntity)
                ? _plan
                : AuditEnrichmentPlan.Empty;
        }
    }

    private sealed class DbCommandCounterInterceptor : DbCommandInterceptor
    {
        public int ReaderCommands { get; private set; }

        public void Reset()
        {
            ReaderCommands = 0;
        }

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            ReaderCommands++;
            return result;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            ReaderCommands++;
            return ValueTask.FromResult(result);
        }
    }

    private sealed class FirstBeforeMergeEnricher : AuditEntityEnricherBase
    {
        public override bool CanHandle(Type entityType) => entityType == typeof(FirstSourceEntity);

        protected override Task BeforeMergeAsync(
            AuditEnrichmentContext context,
            CancellationToken cancellationToken = default)
        {
            var change = Assert.Single(context.GetChangesOf(typeof(FirstSourceEntity)));
            context.GetBagFor(change).SetNew("First", "first");

            return Task.CompletedTask;
        }
    }

    private sealed class SecondBeforeMergeEnricher : AuditEntityEnricherBase
    {
        public override bool CanHandle(Type entityType) => entityType == typeof(FirstSourceEntity);

        protected override Task BeforeMergeAsync(
            AuditEnrichmentContext context,
            CancellationToken cancellationToken = default)
        {
            var change = Assert.Single(context.GetChangesOf(typeof(FirstSourceEntity)));
            var bag = context.GetBagFor(change);

            Assert.True(bag.TryGetNewValue("First", out var firstValue));
            Assert.Equal("first", firstValue);
            Assert.False(change.NewValues.ContainsKey("First"));

            bag.SetNew("Second", "second");

            return Task.CompletedTask;
        }
    }

    private sealed class AfterMergeEnricher : AuditEntityEnricherBase
    {
        public override bool CanHandle(Type entityType) => entityType == typeof(FirstSourceEntity);

        protected override Task AfterMergeAsync(
            AuditEnrichmentContext context,
            CancellationToken cancellationToken = default)
        {
            var change = Assert.Single(context.GetChangesOf(typeof(FirstSourceEntity)));
            var bag = context.GetBagFor(change);

            Assert.False(bag.HasAnyValues());
            Assert.Equal("first", change.NewValues["First"]);
            Assert.Equal("second", change.NewValues["Second"]);

            change.NewValues["After"] = "after";

            return Task.CompletedTask;
        }
    }

    private sealed class PerChangeEnricher : AuditEntityEnricherBase
    {
        public override bool CanHandle(Type entityType) => entityType == typeof(FirstSourceEntity);

        protected override Task BeforeMergeChangeAsync(
            AuditEnrichmentContext context,
            AuditChange change,
            AuditEnrichmentBag bag,
            CancellationToken cancellationToken = default)
        {
            bag.SetNew("PerChangeBefore", "before");
            return Task.CompletedTask;
        }

        protected override Task AfterMergeChangeAsync(
            AuditEnrichmentContext context,
            AuditChange change,
            CancellationToken cancellationToken = default)
        {
            Assert.Equal("before", change.NewValues["PerChangeBefore"]);
            Assert.False(context.GetBagFor(change).HasAnyValues());

            change.NewValues["PerChangeAfter"] = "after";
            return Task.CompletedTask;
        }
    }
}
