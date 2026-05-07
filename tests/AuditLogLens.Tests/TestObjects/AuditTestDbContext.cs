using Microsoft.EntityFrameworkCore;

namespace AuditLogLens.Tests.TestObjects;

public sealed class AuditTestDbContext : DbContext
{
    public AuditTestDbContext(DbContextOptions<AuditTestDbContext> options)
        : base(options)
    {
    }

    public DbSet<AllowedEntity> AllowedEntities => Set<AllowedEntity>();
    public DbSet<DomainConfiguredSourceEntity> DomainConfiguredSourceEntities => Set<DomainConfiguredSourceEntity>();
    public DbSet<ForbiddenEntity> ForbiddenEntities => Set<ForbiddenEntity>();
    public DbSet<FirstSourceEntity> FirstSourceEntities => Set<FirstSourceEntity>();
    public DbSet<RelatedEntity> RelatedEntities => Set<RelatedEntity>();
    public DbSet<SecondSourceEntity> SecondSourceEntities => Set<SecondSourceEntity>();
    public DbSet<SpecialDeleteEntity> SpecialDeleteEntities => Set<SpecialDeleteEntity>();
    public DbSet<TestAuditEntry> TestAuditEntries => Set<TestAuditEntry>();
}