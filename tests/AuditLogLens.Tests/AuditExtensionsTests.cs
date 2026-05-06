using AuditLogLens.Tests.TestObjects;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AuditLogLens.Tests;

public class AuditExtensionsTests
{
    [Fact]
    public async Task AddAuditRestrictions_UsesConfiguredRestrictionsInsteadOfDefaultRules()
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

        db.ForbiddenEntities.Add(new ForbiddenEntity { Value = "ignored" });

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Empty(await db.TestAuditEntries.ToListAsync(TestContext.Current.CancellationToken));
    }
}