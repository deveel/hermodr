//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Deveel.Events;

/// <summary>
/// Entity Framework Core configuration for <see cref="DbDeadLetterAttribute"/>.
/// </summary>
public class DbDeadLetterAttributeConfiguration : IEntityTypeConfiguration<DbDeadLetterAttribute>
{
    public void Configure(EntityTypeBuilder<DbDeadLetterAttribute> builder)
    {
        builder.ToTable("dead_letter_message_attributes");
        builder.HasKey(attribute => attribute.Id);

        builder.Property(attribute => attribute.Id)
            .IsRequired()
            .ValueGeneratedOnAdd();

        builder.Property(attribute => attribute.MessageId)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(attribute => attribute.Name)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(attribute => attribute.Value)
            .IsRequired(false);

        builder.Property(attribute => attribute.ValueType)
            .IsRequired()
            .HasMaxLength(32)
            .HasDefaultValue("string");

        builder.HasIndex(attribute => attribute.MessageId)
            .HasDatabaseName("IX_DeadLetterMessageAttributes_MessageId");

        builder.HasIndex(attribute => new { attribute.MessageId, attribute.Name })
            .IsUnique()
            .HasDatabaseName("UX_DeadLetterMessageAttributes_MessageId_Name");

        builder.HasOne(attribute => attribute.Message)
            .WithMany(message => message.Attributes)
            .HasForeignKey(attribute => attribute.MessageId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
