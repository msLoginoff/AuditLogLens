using AuditLogLens.Tests.TestObjects;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AuditLogLens.Tests;

public class AuditTransactionTests
{
    [Fact]
    public async Task SaveChangesAsync_WhenTransactionalAuditWriterFails_RollsBackPrimaryChanges()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        var services = new ServiceCollection();
        services
            .AddAuditInfrastructure(options => options.WriteMode = AuditWriteMode.Transactional)
            .AddEfAuditWriter<TestAuditEntry, ThrowingAuditEntryMapper>()
            .AddAuditRestrictions<TestAuditRestrictions>();

        await using var serviceProvider = services.BuildServiceProvider();

        var auditedOptionsBuilder = new DbContextOptionsBuilder<AuditTestDbContext>()
            .UseSqlite(connection);
        auditedOptionsBuilder.AddAuditInterceptor(serviceProvider);

        await using (var db = new AuditTestDbContext(auditedOptionsBuilder.Options))
        {
            await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
            db.AllowedEntities.Add(new AllowedEntity { Name = "ShouldRollback" });

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                db.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        var verificationOptions = new DbContextOptionsBuilder<AuditTestDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var verificationDb = new AuditTestDbContext(verificationOptions);
        Assert.Empty(await verificationDb.AllowedEntities.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await verificationDb.TestAuditEntries.ToListAsync(TestContext.Current.CancellationToken));
    }

    private sealed class ThrowingAuditEntryMapper : IAuditEntryMapper<TestAuditEntry>
    {
        public bool CanMap(DbContext dbContext) => true;

        public TestAuditEntry Map(AuditChange change, DbContext dbContext)
        {
            throw new InvalidOperationException("Audit mapper failed.");
        }
    }
}