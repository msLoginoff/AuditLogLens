using Microsoft.EntityFrameworkCore;

namespace AuditLogLens;

public static class AuditLogLensModelBuilderExtensions
{
    public static ModelBuilder UseAuditLogLens(
        this ModelBuilder modelBuilder,
        string tableName = AuditLogLensEntry.DefaultTableName,
        string? schema = null)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

        modelBuilder.Entity<AuditLogLensEntry>(entity =>
        {
            entity.ToTable(tableName, schema);

            entity.HasKey(x => x.Id);

            entity.Property(x => x.TableName)
                .IsRequired()
                .HasMaxLength(512);

            entity.Property(x => x.State)
                .IsRequired()
                .HasMaxLength(64);

            entity.Property(x => x.CreatedAtUtc)
                .IsRequired();

            entity.HasIndex(x => x.CreatedAtUtc);
        });

        return modelBuilder;
    }
}