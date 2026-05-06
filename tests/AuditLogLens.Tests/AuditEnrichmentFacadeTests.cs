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