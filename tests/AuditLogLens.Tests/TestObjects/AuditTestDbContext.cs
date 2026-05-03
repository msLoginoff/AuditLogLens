using Microsoft.EntityFrameworkCore;

namespace AuditLogLens.Tests.TestObjects;

public sealed class AuditTestDbContext : DbContext
{
    public AuditTestDbContext(DbContextOptions<AuditTestDbContext> options)
        : base(options)
    {
    }

    public DbSet<AllowedEntity> AllowedEntities => Set<AllowedEntity>();
    public DbSet<ForbiddenEntity> ForbiddenEntities => Set<ForbiddenEntity>();
    public DbSet<RelatedEntity> RelatedEntities => Set<RelatedEntity>();
    public DbSet<SpecialDeleteEntity> SpecialDeleteEntities => Set<SpecialDeleteEntity>();
    public DbSet<TestAuditEntry> TestAuditEntries => Set<TestAuditEntry>();
}