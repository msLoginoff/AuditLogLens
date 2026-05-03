using AuditLogLens.Abstractions;
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

    private sealed class TestAuditEntryMapper : IAuditEntryMapper<TestAuditEntry>
    {
        public bool CanMap(DbContext dbContext) => dbContext is AuditTestDbContext;

        public TestAuditEntry Map(AuditChange change, DbContext dbContext)
        {
            return new TestAuditEntry
            {
                TableName = change.TableName,
                EntityId = change.EntityId?.ToString(),
                State = change.State,
                NewName = change.NewValues.TryGetValue(nameof(AllowedEntity.Name), out var name)
                    ? name?.ToString()
                    : null
            };
        }
    }
}