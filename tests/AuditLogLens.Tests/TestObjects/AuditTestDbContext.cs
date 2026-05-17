using Microsoft.EntityFrameworkCore;

namespace AuditLogLens.Tests.TestObjects;

public sealed class AuditTestDbContext : DbContext
{
    public AuditTestDbContext(DbContextOptions<AuditTestDbContext> options)
        : base(options)
    {
    }

    public DbSet<AllowedEntity> AllowedEntities => Set<AllowedEntity>();
    public DbSet<CollectionLookupEntity> CollectionLookupEntities => Set<CollectionLookupEntity>();
    public DbSet<CollectionParentEntity> CollectionParentEntities => Set<CollectionParentEntity>();
    public DbSet<CollectionRefEntity> CollectionRefEntities => Set<CollectionRefEntity>();
    public DbSet<DomainConfiguredSourceEntity> DomainConfiguredSourceEntities => Set<DomainConfiguredSourceEntity>();
    public DbSet<ForbiddenEntity> ForbiddenEntities => Set<ForbiddenEntity>();
    public DbSet<FirstSourceEntity> FirstSourceEntities => Set<FirstSourceEntity>();
    public DbSet<NestedRelatedEntity> NestedRelatedEntities => Set<NestedRelatedEntity>();
    public DbSet<PolymorphicAbsenceEvent> PolymorphicAbsenceEvents => Set<PolymorphicAbsenceEvent>();
    public DbSet<PolymorphicCollectionEvent> PolymorphicCollectionEvents => Set<PolymorphicCollectionEvent>();

    public DbSet<PolymorphicCollectionRefEntity> PolymorphicCollectionRefEntities =>
        Set<PolymorphicCollectionRefEntity>();

    public DbSet<PolymorphicVisitEvent> PolymorphicVisitEvents => Set<PolymorphicVisitEvent>();
    public DbSet<RelatedEntity> RelatedEntities => Set<RelatedEntity>();
    public DbSet<SecondSourceEntity> SecondSourceEntities => Set<SecondSourceEntity>();
    public DbSet<SpecialDeleteEntity> SpecialDeleteEntities => Set<SpecialDeleteEntity>();
    public DbSet<TestAuditEntry> TestAuditEntries => Set<TestAuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PolymorphicCollectionEvent>()
            .HasDiscriminator<string>("EventType")
            .HasValue<PolymorphicVisitEvent>("Visit")
            .HasValue<PolymorphicAbsenceEvent>("Absence");

        modelBuilder.Entity<PolymorphicCollectionRefEntity>()
            .HasKey(x => new { x.EventId, x.LookupId });
    }
}