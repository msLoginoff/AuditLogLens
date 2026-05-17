using AuditLogLens.Enrichment.Internal;
using AuditLogLens.Enrichment.Internal.Planning;
using AuditLogLens.Tests.TestObjects;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AuditLogLens.Tests;

public class AuditDomainEnrichmentConfigTests
{
    [Fact]
    public async Task EnrichAsync_UsesDeclarativeDomainConfig()
    {
        await using var db = CreateDbContext();

        db.RelatedEntities.Add(new RelatedEntity { Id = 10, Name = "Readable" });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var change = new AuditChange
        {
            EntityType = typeof(DomainConfiguredSourceEntity),
            State = nameof(EntityState.Added)
        };
        change.NewValues[nameof(DomainConfiguredSourceEntity.RelatedEntityId)] = 10;

        var enricherRegistry = new AuditEntityEnricherRegistry([]);
        var enricher = new AuditEnrichmentFacade(
            new AuditEnrichmentPlanResolver(
                new StaticAuditDomainEnrichmentPlanProvider(),
                enricherRegistry),
            enricherRegistry);

        await enricher.EnrichAsync([change], db, [], TestContext.Current.CancellationToken);

        Assert.Equal("Readable", change.NewValues["RelatedName"]);
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
}