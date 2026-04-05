using Microsoft.EntityFrameworkCore;

namespace AuditLog.Tests.TestObjects;

public sealed class AuditTestDbContext : DbContext
{
    public AuditTestDbContext(DbContextOptions<AuditTestDbContext> options)
        : base(options)
    {
    }

    public DbSet<AllowedEntity> AllowedEntities => Set<AllowedEntity>();
    public DbSet<ForbiddenEntity> ForbiddenEntities => Set<ForbiddenEntity>();
    public DbSet<SpecialDeleteEntity> SpecialDeleteEntities => Set<SpecialDeleteEntity>();
}