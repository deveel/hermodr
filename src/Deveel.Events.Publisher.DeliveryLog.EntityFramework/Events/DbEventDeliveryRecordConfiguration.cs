using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Deveel.Events;

/// <summary>
/// Configures the Entity Framework Core mapping for the <see cref="DbEventDeliveryRecord"/> entity.
/// </summary>
public class DbEventDeliveryRecordConfiguration : IEntityTypeConfiguration<DbEventDeliveryRecord>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<DbEventDeliveryRecord> builder)
    {
        builder.ToTable("delivery_records");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).IsRequired().HasMaxLength(256).ValueGeneratedNever();
        builder.Property(r => r.EventId).IsRequired().HasMaxLength(256);
        builder.Property(r => r.EventType).HasMaxLength(256).IsRequired(false);
        builder.Property(r => r.EventData).IsRequired(false);
        builder.Property(r => r.PublisherName).HasMaxLength(256).IsRequired(false);
        builder.Property(r => r.ChannelName).HasMaxLength(256).IsRequired(false);
        builder.Property(r => r.ChannelType).HasMaxLength(256).IsRequired(false);
        builder.Property(r => r.AttemptNumber).IsRequired().HasDefaultValue(1);
        builder.Property(r => r.Timestamp).IsRequired()
            .HasConversion(dto => dto.UtcDateTime, dt => new DateTimeOffset(dt, TimeSpan.Zero));
        builder.Property(r => r.Outcome).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(r => r.ErrorCode).HasMaxLength(128).IsRequired(false);
        builder.Property(r => r.ErrorMessage).IsRequired(false);
        builder.Property(r => r.ElapsedTimeTicks).IsRequired().HasDefaultValue(0L);

        builder.HasIndex(r => r.EventId).HasDatabaseName("IX_DeliveryRecords_EventId");
        builder.HasIndex(r => r.ChannelName).HasDatabaseName("IX_DeliveryRecords_ChannelName");
        builder.HasIndex(r => r.Outcome).HasDatabaseName("IX_DeliveryRecords_Outcome");
        builder.HasIndex(r => r.Timestamp).HasDatabaseName("IX_DeliveryRecords_Timestamp");
    }
}
