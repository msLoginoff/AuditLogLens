using AuditLogLens.Writing;
using Microsoft.EntityFrameworkCore;

namespace AuditLogLens;

/// <summary>
/// Provides EF Core model configuration helpers for the default AuditLogLens model.
/// </summary>
public static class AuditLogLensModelBuilderExtensions
{
    /// <summary>
    /// Adds the default <see cref="AuditLogLensEntry"/> entity to the EF Core model.
    /// </summary>
    /// <param name="modelBuilder">The model builder to configure.</param>
    /// <param name="tableName">The table name used for audit entries.</param>
    /// <param name="schema">The optional database schema used for audit entries.</param>
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
