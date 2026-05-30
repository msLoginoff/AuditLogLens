using AuditLogLens.Changes;
using AuditLogLens.Manual;
using AuditLogLens.Pipeline;
using AuditLogLens.Tests.TestObjects;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AuditLogLens.Tests;

public class ManualAuditPipelineTests
{
    [Fact]
    public async Task ProcessAsync_ByDefault_AddsAuditEntryWithoutSavingCurrentContext()
    {
        await using var connection = await OpenConnectionAsync();
        await using var serviceProvider = CreateServiceProvider();
        await using var db = await CreateDbContextAsync(connection);

        var businessEntity = new AllowedEntity { Name = "Business" };
        db.AllowedEntities.Add(businessEntity);

        var change = CreateFactory(serviceProvider).CreateManual(
            tableName: nameof(AllowedEntity),
            rowKey: "manual-1",
            state: AuditChangeState.Added,
            newValues: new Dictionary<string, object?>
            {
                [nameof(AllowedEntity.Name)] = "Manual"
            });

        await CreatePipeline(serviceProvider).ProcessAsync(
            db,
            [change],
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(EntityState.Added, db.Entry(businessEntity).State);
        Assert.Empty(await db.TestAuditEntries.AsNoTracking().ToListAsync(TestContext.Current.CancellationToken));

        var pendingAuditEntry = Assert.Single(db.ChangeTracker.Entries<TestAuditEntry>());
        Assert.Equal(EntityState.Added, pendingAuditEntry.State);

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var auditEntry = await db.TestAuditEntries.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(nameof(AllowedEntity), auditEntry.TableName);
        Assert.Equal("manual-1", auditEntry.EntityId);
        Assert.Equal(nameof(AuditChangeState.Added), auditEntry.State);
        Assert.Equal("Manual", auditEntry.NewName);
        Assert.Equal(1, await db.AllowedEntities.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ProcessAsync_WithSaveImmediately_SavesAuditEntryAndCurrentPendingChanges()
    {
        await using var connection = await OpenConnectionAsync();
        await using var serviceProvider = CreateServiceProvider();
        await using var db = await CreateDbContextAsync(connection);

        var businessEntity = new AllowedEntity { Name = "Business" };
        db.AllowedEntities.Add(businessEntity);

        var change = CreateFactory(serviceProvider).CreateManual(
            tableName: nameof(AllowedEntity),
            rowKey: "manual-2",
            state: AuditChangeState.Added,
            newValues: new Dictionary<string, object?>
            {
                [nameof(AllowedEntity.Name)] = "Saved immediately"
            });

        await CreatePipeline(serviceProvider).ProcessAsync(
            db,
            [change],
            new AuditPipelineSettings { SaveBehavior = AuditSaveBehavior.SaveImmediately },
            TestContext.Current.CancellationToken);

        Assert.Equal(EntityState.Unchanged, db.Entry(businessEntity).State);
        Assert.Equal(1, await db.AllowedEntities.CountAsync(TestContext.Current.CancellationToken));

        var auditEntry = await db.TestAuditEntries.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("manual-2", auditEntry.EntityId);
        Assert.Equal("Saved immediately", auditEntry.NewName);
    }

    [Fact]
    public async Task ProcessAsync_ForManualChange_DoesNotApplyAuditRestrictions()
    {
        await using var connection = await OpenConnectionAsync();
        await using var serviceProvider = CreateServiceProvider();
        await using var db = await CreateDbContextAsync(connection);

        var change = CreateFactory(serviceProvider).CreateManual(
            tableName: nameof(ForbiddenEntity),
            rowKey: 10,
            state: AuditChangeState.Added,
            newValues: new Dictionary<string, object?>
            {
                [nameof(ForbiddenEntity.Value)] = "Explicit manual value"
            });

        await CreatePipeline(serviceProvider).ProcessAsync(
            db,
            [change],
            cancellationToken: TestContext.Current.CancellationToken);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var auditEntry = await db.TestAuditEntries.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(nameof(ForbiddenEntity), auditEntry.TableName);
        Assert.Equal("10", auditEntry.EntityId);
        Assert.Equal(nameof(AuditChangeState.Added), auditEntry.State);
    }

    [Fact]
    public async Task ProcessAsync_EnrichesManualReferenceFromValueDictionaries()
    {
        await using var connection = await OpenConnectionAsync();
        await using var serviceProvider = CreateServiceProvider();
        await using var db = await CreateDbContextAsync(connection);

        db.RelatedEntities.Add(new RelatedEntity { Id = 7, Name = "Readable related value" });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var change = CreateFactory(serviceProvider).CreateManual(
            tableName: nameof(DomainConfiguredSourceEntity),
            rowKey: 1,
            state: AuditChangeState.Added,
            newValues: new Dictionary<string, object?>
            {
                [nameof(DomainConfiguredSourceEntity.RelatedEntityId)] = 7
            },
            sourceType: typeof(DomainConfiguredSourceEntity));

        await CreatePipeline(serviceProvider).ProcessAsync(
            db,
            [change],
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("Readable related value", change.NewValues["RelatedName"]);
    }

    [Fact]
    public async Task ProcessAsync_EnrichesManualModifiedReferenceForOldAndNewValues()
    {
        await using var connection = await OpenConnectionAsync();
        await using var serviceProvider = CreateServiceProvider();
        await using var db = await CreateDbContextAsync(connection);

        db.RelatedEntities.AddRange(
            new RelatedEntity { Id = 7, Name = "Old related value" },
            new RelatedEntity { Id = 8, Name = "New related value" });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var change = CreateFactory(serviceProvider).CreateManual(
            tableName: nameof(DomainConfiguredSourceEntity),
            rowKey: 1,
            state: AuditChangeState.Modified,
            newValues: new Dictionary<string, object?>
            {
                [nameof(DomainConfiguredSourceEntity.RelatedEntityId)] = 8
            },
            oldValues: new Dictionary<string, object?>
            {
                [nameof(DomainConfiguredSourceEntity.RelatedEntityId)] = 7
            },
            sourceType: typeof(DomainConfiguredSourceEntity));

        await CreatePipeline(serviceProvider).ProcessAsync(
            db,
            [change],
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("Old related value", change.OldValues["RelatedName"]);
        Assert.Equal("New related value", change.NewValues["RelatedName"]);
    }

    [Fact]
    public async Task ProcessAsync_WithManualCollectionChangeAndNoEntry_DoesNotRequireChangeTrackerEntry()
    {
        await using var connection = await OpenConnectionAsync();
        await using var serviceProvider = CreateServiceProvider();
        await using var db = await CreateDbContextAsync(connection);

        var change = CreateFactory(serviceProvider).CreateManual(
            tableName: nameof(CollectionParentEntity),
            rowKey: 100,
            state: AuditChangeState.Modified,
            newValues: new Dictionary<string, object?>
            {
                [nameof(CollectionParentEntity.Name)] = "Manual collection parent"
            },
            sourceType: typeof(CollectionParentEntity));

        await CreatePipeline(serviceProvider).ProcessAsync(
            db,
            [change],
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(change.NewValues.ContainsKey("Tags"));

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var auditEntry = await db.TestAuditEntries.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(nameof(CollectionParentEntity), auditEntry.TableName);
        Assert.Equal("100", auditEntry.EntityId);
    }

    private static async Task<SqliteConnection> OpenConnectionAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        return connection;
    }

    private static ServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        services
            .AddAuditInfrastructure()
            .AddEfAuditWriter<TestAuditEntry, TestAuditEntryMapper>()
            .AddAuditRestrictions<TestAuditRestrictions>();

        return services.BuildServiceProvider();
    }

    private static async Task<AuditTestDbContext> CreateDbContextAsync(SqliteConnection connection)
    {
        var db = new AuditTestDbContext(
            new DbContextOptionsBuilder<AuditTestDbContext>()
                .UseSqlite(connection)
                .Options);

        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        return db;
    }

    private static IAuditChangeFactory CreateFactory(IServiceProvider serviceProvider)
    {
        return serviceProvider.GetRequiredService<IAuditChangeFactory>();
    }

    private static IAuditPipeline CreatePipeline(IServiceProvider serviceProvider)
    {
        return serviceProvider.GetRequiredService<IAuditPipeline>();
    }
}
