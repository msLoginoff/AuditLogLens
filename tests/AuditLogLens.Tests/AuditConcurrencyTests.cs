using AuditLogLens.Tests.TestObjects;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AuditLogLens.Tests;

public class AuditConcurrencyTests
{
    [Fact]
    public async Task SaveChangesAsync_WithSharedServiceProviderAndParallelDbContexts_WritesIndependentAuditEntries()
    {
        var services = new ServiceCollection();
        services
            .AddAuditInfrastructure()
            .AddEfAuditWriter<TestAuditEntry, TestAuditEntryMapper>()
            .AddAuditRestrictions<TestAuditRestrictions>();

        await using var serviceProvider = services.BuildServiceProvider();
        var cancellationToken = TestContext.Current.CancellationToken;

        var results = await Task.WhenAll(
            Enumerable.Range(1, 8)
                .Select(index => SaveOneEntityAsync(serviceProvider, index, cancellationToken)));

        Assert.Equal(
            Enumerable.Range(1, 8).Select(index => $"Name-{index}").OrderBy(x => x),
            results.OrderBy(x => x));
    }

    private static async Task<string?> SaveOneEntityAsync(
        ServiceProvider serviceProvider,
        int index,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync(cancellationToken);

        await using var scope = serviceProvider.CreateAsyncScope();

        var optionsBuilder = new DbContextOptionsBuilder<AuditTestDbContext>()
            .UseSqlite(connection);
        optionsBuilder.AddAuditInterceptor(scope.ServiceProvider);

        await using var db = new AuditTestDbContext(optionsBuilder.Options);
        await db.Database.EnsureCreatedAsync(cancellationToken);

        db.AllowedEntities.Add(new AllowedEntity { Name = $"Name-{index}" });
        await db.SaveChangesAsync(cancellationToken);

        return await db.TestAuditEntries
            .Select(x => x.NewName)
            .SingleAsync(cancellationToken);
    }
}