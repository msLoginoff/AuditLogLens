using AuditLogLens.Restrictions.Internal;
using AuditLogLens.Tests.TestObjects;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AuditLogLens.Tests;

public class EfAuditWriterTests
{
    [Fact]
    public async Task SaveChangesAsync_WithConfiguredEfAuditWriter_WritesMappedAuditEntry()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        var services = new ServiceCollection();
        services
            .AddAuditInfrastructure()
            .AddEfAuditWriter<TestAuditEntry, TestAuditEntryMapper>();
        services.AddSingleton<IAuditRestrictions, TestAuditRestrictions>();

        await using var serviceProvider = services.BuildServiceProvider();

        var optionsBuilder = new DbContextOptionsBuilder<AuditTestDbContext>()
            .UseSqlite(connection);
        optionsBuilder.AddAuditInterceptor(serviceProvider);
        var options = optionsBuilder.Options;

        await using var db = new AuditTestDbContext(options);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        db.AllowedEntities.Add(new AllowedEntity
        {
            Name = "John",
            Secret = "hidden"
        });

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var auditEntry = await db.TestAuditEntries.SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(nameof(AllowedEntity), auditEntry.TableName);
        Assert.Equal(nameof(EntityState.Added), auditEntry.State);
        Assert.Equal("John", auditEntry.NewName);
        Assert.True(int.Parse(auditEntry.EntityId!) > 0);
    }

    [Fact]
    public async Task SaveChangesAsync_WhenOnlyIgnoredPropertyChanged_DoesNotWriteEmptyAuditEntry()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        var services = new ServiceCollection();
        services
            .AddAuditInfrastructure()
            .AddEfAuditWriter<TestAuditEntry, TestAuditEntryMapper>()
            .AddAuditRestrictions<TestAuditRestrictions>();

        await using var serviceProvider = services.BuildServiceProvider();

        var optionsBuilder = new DbContextOptionsBuilder<AuditTestDbContext>()
            .UseSqlite(connection);
        optionsBuilder.AddAuditInterceptor(serviceProvider);
        var options = optionsBuilder.Options;

        await using var db = new AuditTestDbContext(options);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        db.AllowedEntities.Add(new AllowedEntity
        {
            Name = "John",
            Secret = "hidden"
        });

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.TestAuditEntries.RemoveRange(db.TestAuditEntries);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var entity = await db.AllowedEntities.SingleAsync(TestContext.Current.CancellationToken);
        entity.Secret = "changed";

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Empty(await db.TestAuditEntries.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SaveChangesAsync_WhenOnlyIgnoredPropertyChangedAndDefaultAuditModelIsNotConfigured_DoesNotThrow()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        var plainOptions = new DbContextOptionsBuilder<AuditTestDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var seedDb = new AuditTestDbContext(plainOptions))
        {
            await seedDb.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
            seedDb.AllowedEntities.Add(new AllowedEntity
            {
                Name = "John",
                Secret = "hidden"
            });
            await seedDb.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var services = new ServiceCollection();
        services
            .AddAuditInfrastructure()
            .AddAuditRestrictions<TestAuditRestrictions>();

        await using var serviceProvider = services.BuildServiceProvider();

        var auditedOptionsBuilder = new DbContextOptionsBuilder<AuditTestDbContext>()
            .UseSqlite(connection);
        auditedOptionsBuilder.AddAuditInterceptor(serviceProvider);

        await using var db = new AuditTestDbContext(auditedOptionsBuilder.Options);
        var entity = await db.AllowedEntities.SingleAsync(TestContext.Current.CancellationToken);
        entity.Secret = "changed";

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task SaveChangesAsync_WhenOnlyCollectionChanged_WritesEnrichedAuditEntry()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        var services = new ServiceCollection();
        services
            .AddAuditInfrastructure()
            .AddEfAuditWriter<TestAuditEntry, TestAuditEntryMapper>()
            .AddAuditRestrictions<TestAuditRestrictions>();

        await using var serviceProvider = services.BuildServiceProvider();

        var optionsBuilder = new DbContextOptionsBuilder<AuditTestDbContext>()
            .UseSqlite(connection);
        optionsBuilder.AddAuditInterceptor(serviceProvider);

        await using var db = new AuditTestDbContext(optionsBuilder.Options);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        db.CollectionLookupEntities.Add(new CollectionLookupEntity { Id = 10, Name = "Tracked Tag" });
        db.CollectionParentEntities.Add(new CollectionParentEntity { Name = "Parent" });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        db.TestAuditEntries.RemoveRange(db.TestAuditEntries);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.ChangeTracker.Clear();

        var parentId = await db.CollectionParentEntities
            .Select(x => x.Id)
            .SingleAsync(TestContext.Current.CancellationToken);

        db.CollectionRefEntities.Add(new CollectionRefEntity
        {
            ParentId = parentId,
            LookupId = 10
        });

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var auditEntry = await db.TestAuditEntries.SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(nameof(CollectionParentEntity), auditEntry.TableName);
        Assert.Equal(nameof(EntityState.Modified), auditEntry.State);
        Assert.Null(auditEntry.NewName);
        Assert.Equal("Tracked Tag", auditEntry.NewTags);
    }

    [Fact]
    public async Task SaveChangesAsync_WhenPolymorphicCollectionParentIsNotTracked_DoesNotPromoteByScalarKey()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        var services = new ServiceCollection();
        services
            .AddAuditInfrastructure()
            .AddEfAuditWriter<TestAuditEntry, TestAuditEntryMapper>()
            .AddAuditRestrictions<TestAuditRestrictions>();

        await using var serviceProvider = services.BuildServiceProvider();

        var optionsBuilder = new DbContextOptionsBuilder<AuditTestDbContext>()
            .UseSqlite(connection);
        optionsBuilder.AddAuditInterceptor(serviceProvider);

        await using var db = new AuditTestDbContext(optionsBuilder.Options);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        db.CollectionLookupEntities.Add(new CollectionLookupEntity { Id = 10, Name = "Tracked Tag" });
        db.PolymorphicAbsenceEvents.Add(new PolymorphicAbsenceEvent { Reason = "Vacation" });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        db.TestAuditEntries.RemoveRange(db.TestAuditEntries);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.ChangeTracker.Clear();

        var absenceId = await db.PolymorphicAbsenceEvents
            .Select(x => x.Id)
            .SingleAsync(TestContext.Current.CancellationToken);

        db.PolymorphicCollectionRefEntities.Add(new PolymorphicCollectionRefEntity
        {
            EventId = absenceId,
            LookupId = 10
        });

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Empty(await db.TestAuditEntries.ToListAsync(TestContext.Current.CancellationToken));
    }
}