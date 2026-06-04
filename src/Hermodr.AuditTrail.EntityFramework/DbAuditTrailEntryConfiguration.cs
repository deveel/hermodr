using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hermodr;

/// <summary>
/// Configures the Entity Framework Core mapping for the <see cref="DbAuditTrailEntry"/> entity.
/// </summary>
public class DbAuditTrailEntryConfiguration : IEntityTypeConfiguration<DbAuditTrailEntry>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<DbAuditTrailEntry> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).IsRequired().HasMaxLength(256).ValueGeneratedNever();
        builder.Property(e => e.EventId).IsRequired().HasMaxLength(256);
        builder.Property(e => e.EventType).IsRequired().HasMaxLength(256);
        builder.Property(e => e.EventDataClassName).HasMaxLength(512).IsRequired(false);
        builder.Property(e => e.Source).IsRequired().HasMaxLength(1024);
        builder.Property(e => e.Subject).HasMaxLength(512).IsRequired(false);
        builder.Property(e => e.Timestamp).IsRequired()
            .HasConversion(dto => dto.UtcDateTime, dt => new DateTimeOffset(dt, TimeSpan.Zero));
        builder.Property(e => e.DataContentType).HasMaxLength(256).IsRequired(false);
        builder.Property(e => e.DataSchema).HasMaxLength(1024).IsRequired(false);
        builder.Property(e => e.EventData).IsRequired(false);
        builder.Property(e => e.StoredAt).IsRequired()
            .HasConversion(dto => dto.UtcDateTime, dt => new DateTimeOffset(dt, TimeSpan.Zero));

        builder.HasIndex(e => e.EventId);
        builder.HasIndex(e => e.EventType);
        builder.HasIndex(e => e.EventDataClassName);
        builder.HasIndex(e => e.Source);
        builder.HasIndex(e => e.Subject);
        builder.HasIndex(e => e.Timestamp);
        builder.HasIndex(e => e.StoredAt);
    }
}
