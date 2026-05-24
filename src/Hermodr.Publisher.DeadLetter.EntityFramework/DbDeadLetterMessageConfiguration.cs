//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hermodr;

/// <summary>
/// Entity Framework Core configuration for <see cref="DbDeadLetterMessage"/>.
/// </summary>
public class DbDeadLetterMessageConfiguration : IEntityTypeConfiguration<DbDeadLetterMessage>
{
    public void Configure(EntityTypeBuilder<DbDeadLetterMessage> builder)
    {
        builder.ToTable("dead_letter_messages");

        builder.HasKey(message => message.Id);

        builder.Property(message => message.Id)
            .IsRequired()
            .HasMaxLength(256)
            .ValueGeneratedNever();

        builder.Property(message => message.SpecVersion)
            .IsRequired()
            .HasMaxLength(10)
            .HasDefaultValue(CloudEventsSpecVersion.V1_0.VersionId);

        builder.Property(message => message.EventType)
            .IsRequired()
            .HasMaxLength(512)
            .HasColumnName("Type");

        builder.Property(message => message.Source)
            .IsRequired()
            .HasMaxLength(2048);

        builder.Property(message => message.Subject)
            .HasMaxLength(1024)
            .IsRequired(false);

        builder.Property(message => message.EventTime)
            .IsRequired(false)
            .HasConversion(
                dto => dto.HasValue ? (DateTime?)dto.Value.UtcDateTime : null,
                dt => dt.HasValue ? new DateTimeOffset(dt.Value, TimeSpan.Zero) : null);

        builder.Property(message => message.DataContentType)
            .HasMaxLength(256)
            .IsRequired(false);

        builder.Property(message => message.DataSchema)
            .HasMaxLength(2048)
            .IsRequired(false);

        builder.Property(message => message.DataText)
            .IsRequired(false);

        builder.Property(message => message.DataBytes)
            .IsRequired(false);

        builder.Property(message => message.PublisherName)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(message => message.ChannelName)
            .IsRequired(false)
            .HasMaxLength(256);

        builder.Property(message => message.ChannelType)
            .IsRequired(false)
            .HasMaxLength(2048);

        builder.Property(message => message.ErrorMessage)
            .IsRequired(false);

        builder.Property(message => message.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(message => message.ReplayCount)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(message => message.NextReplayAt)
            .IsRequired(false)
            .HasConversion(
                dto => dto.HasValue ? (DateTime?)dto.Value.UtcDateTime : null,
                dt => dt.HasValue ? new DateTimeOffset(dt.Value, TimeSpan.Zero) : null);

        builder.Property(message => message.CreatedAt)
            .IsRequired()
            .HasConversion(
                dto => dto.UtcDateTime,
                dt => new DateTimeOffset(dt, TimeSpan.Zero));

        builder.Property(message => message.LastStatusAt)
            .IsRequired(false)
            .HasConversion(
                dto => dto.HasValue ? (DateTime?)dto.Value.UtcDateTime : null,
                dt => dt.HasValue ? new DateTimeOffset(dt.Value, TimeSpan.Zero) : null);

        builder.HasIndex(message => new { message.Status, message.NextReplayAt })
            .HasDatabaseName("IX_DeadLetterMessages_Status_NextReplayAt");

        builder.HasMany(message => message.Attributes)
            .WithOne(attribute => attribute.Message)
            .HasForeignKey(attribute => attribute.MessageId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
