using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hermodr;

/// <summary>
/// Configures the Entity Framework Core mapping for the <see cref="DbAuditTrailAttribute"/> entity.
/// </summary>
public class DbAuditTrailAttributeConfiguration : IEntityTypeConfiguration<DbAuditTrailAttribute>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<DbAuditTrailAttribute> builder)
    {
        builder.ToTable("audit_trail_attributes");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).IsRequired().HasMaxLength(256).ValueGeneratedNever();
        builder.Property(a => a.AuditTrailEntryId).IsRequired().HasMaxLength(256);
        builder.Property(a => a.Key).IsRequired().HasMaxLength(256);
        builder.Property(a => a.Value).IsRequired();

        builder.HasIndex(a => new { a.AuditTrailEntryId, a.Key }).HasDatabaseName("IX_AuditTrailAttributes_EntryId_Key");
        builder.HasIndex(a => a.Key).HasDatabaseName("IX_AuditTrailAttributes_Key");

        builder.HasOne<DbAuditTrailEntry>()
            .WithMany(e => e.Attributes)
            .HasForeignKey(a => a.AuditTrailEntryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
