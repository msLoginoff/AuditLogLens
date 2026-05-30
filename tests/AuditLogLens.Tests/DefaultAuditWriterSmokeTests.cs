using System.Text.Json;
using AuditLogLens.Tests.TestObjects;
using AuditLogLens.Writing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AuditLogLens.Tests;

public class DefaultAuditWriterSmokeTests
{
    [Fact]
    public async Task SaveChangesAsync_WithDefaultAuditModel_WritesAuditLogLensEntry()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        var services = new ServiceCollection();
        services
            .AddAuditInfrastructure()
            .AddAuditRestrictions<TestAuditRestrictions>();

        await using var serviceProvider = services.BuildServiceProvider();

        var optionsBuilder = new DbContextOptionsBuilder<DefaultAuditDbContext>()
            .UseSqlite(connection);
        optionsBuilder.AddAuditInterceptor(serviceProvider);

        await using var db = new DefaultAuditDbContext(optionsBuilder.Options);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        db.AllowedEntities.Add(new AllowedEntity
        {
            Name = "John",
            Secret = "hidden"
        });

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var auditEntry = await db.AuditLogLensEntries.SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(nameof(AllowedEntity), auditEntry.TableName);
        Assert.Equal(nameof(EntityState.Added), auditEntry.State);
        Assert.Null(auditEntry.OldValuesJson);
        Assert.NotNull(auditEntry.NewValuesJson);

        using var newValues = JsonDocument.Parse(auditEntry.NewValuesJson);
        Assert.Equal("John", newValues.RootElement.GetProperty(nameof(AllowedEntity.Name)).GetString());
        Assert.False(newValues.RootElement.TryGetProperty(nameof(AllowedEntity.Secret), out _));
    }

    private sealed class DefaultAuditDbContext : DbContext
    {
        public DefaultAuditDbContext(DbContextOptions<DefaultAuditDbContext> options)
            : base(options)
        {
        }

        public DbSet<AllowedEntity> AllowedEntities => Set<AllowedEntity>();

        public DbSet<AuditLogLensEntry> AuditLogLensEntries => Set<AuditLogLensEntry>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.UseAuditLogLens();
        }
    }
}
