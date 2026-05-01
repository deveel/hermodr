//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Deveel.Events;

/// <summary>
/// Entity Framework Core type configuration for <see cref="DbCloudEventAttribute"/>.
/// </summary>
/// <remarks>
/// <para>
/// This configuration is complementary to <see cref="DbOutboxMessageConfiguration"/>
/// and covers the <c>OutboxMessageAttributes</c> table that stores CloudEvents
/// extension attributes in a one-to-many relationship with
/// <see cref="DbOutboxMessage"/>.
/// </para>
/// <para>
/// Apply alongside <see cref="DbOutboxMessageConfiguration"/> inside your
/// <see cref="DbContext.OnModelCreating"/> override, or discover both automatically
/// with <see cref="ModelBuilder.ApplyConfigurationsFromAssembly"/>.
/// </para>
/// </remarks>
public class DbCloudEventAttributeConfiguration : IEntityTypeConfiguration<DbCloudEventAttribute>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<DbCloudEventAttribute> builder)
    {
        // ── Table ─────────────────────────────────────────────────────────────
        builder.ToTable("outbox_message_attributes");

        // ── Primary key ───────────────────────────────────────────────────────
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .IsRequired()
            .ValueGeneratedOnAdd(); // Auto-increment surrogate key

        // ── Foreign key ───────────────────────────────────────────────────────
        builder.Property(a => a.MessageId)
            .IsRequired()
            .HasMaxLength(256);

        // ── Attribute columns ─────────────────────────────────────────────────
        builder.Property(a => a.Name)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(a => a.Value)
            .IsRequired(false);

        builder.Property(a => a.ValueType)
            .IsRequired()
            .HasMaxLength(32)
            .HasDefaultValue("string");

        // ── Indexes ───────────────────────────────────────────────────────────
        // Speeds up loading all attributes for a given message (eager/explicit loading).
        builder.HasIndex(a => a.MessageId)
            .HasDatabaseName("IX_OutboxMessageAttributes_MessageId");

        // Unique constraint: a message must not carry duplicate attribute names.
        builder.HasIndex(a => new { a.MessageId, a.Name })
            .IsUnique()
            .HasDatabaseName("UX_OutboxMessageAttributes_MessageId_Name");

        // ── Relationship (inverse side – FK already owned by HasMany above) ───
        builder.HasOne(a => a.Message)
            .WithMany(m => m.Attributes)
            .HasForeignKey(a => a.MessageId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

