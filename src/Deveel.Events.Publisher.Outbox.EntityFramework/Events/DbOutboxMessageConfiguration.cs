//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Deveel.Events;

/// <summary>
/// Entity Framework Core type configuration for <see cref="DbOutboxMessage"/>.
/// </summary>
/// <remarks>
/// <para>
/// Apply this configuration inside your <see cref="DbContext.OnModelCreating"/> override:
/// </para>
/// <code language="csharp">
/// protected override void OnModelCreating(ModelBuilder modelBuilder)
/// {
///     modelBuilder.ApplyConfiguration(new DbOutboxMessageConfiguration());
/// }
/// </code>
/// <para>
/// Or use <see cref="ModelBuilder.ApplyConfigurationsFromAssembly"/> to discover it
/// automatically.
/// </para>
/// <para>
/// The configuration maps:
/// <list type="bullet">
///   <item>
///     <term><c>OutboxMessages</c> table</term>
///     <description>
///     Scalar columns for all well-known CloudEvents context attributes
///     (<c>Id</c>, <c>SpecVersion</c>, <c>EventType</c>, <c>Source</c>,
///     <c>Subject</c>, <c>EventTime</c>, <c>DataContentType</c>,
///     <c>DataSchema</c>), the data payload columns (<c>DataText</c>,
///     <c>DataBytes</c>), and the outbox delivery-tracking columns
///     (<c>Status</c>, <c>ErrorMessage</c>, <c>RetryCount</c>,
///     <c>NextRetryAt</c>, <c>CreatedAt</c>, <c>LastStatusAt</c>).
///     </description>
///   </item>
///   <item>
///     <term><c>OutboxMessageAttributes</c> table (one-to-many)</term>
///     <description>
///     Child rows owned by <see cref="DbCloudEventAttribute"/> that store
///     CloudEvents extension attributes.  Configured via a cascade-delete
///     relationship so that attribute rows are automatically removed when
///     the parent message is deleted.
///     </description>
///   </item>
/// </list>
/// </para>
/// </remarks>
public class DbOutboxMessageConfiguration : IEntityTypeConfiguration<DbOutboxMessage>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<DbOutboxMessage> builder)
    {
        // ── Table ─────────────────────────────────────────────────────────────
        builder.ToTable("OutboxMessages");

        // ── Primary key ───────────────────────────────────────────────────────
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id)
            .IsRequired()
            .HasMaxLength(256)
            .ValueGeneratedNever(); // The CloudEvent id is set externally

        // ── CloudEvent required context attributes ────────────────────────────
        builder.Property(m => m.SpecVersion)
            .IsRequired()
            .HasMaxLength(10)
            .HasDefaultValue(CloudEventsSpecVersion.V1_0.VersionId);

        builder.Property(m => m.EventType)
            .IsRequired()
            .HasMaxLength(512)
            .HasColumnName("Type");

        builder.Property(m => m.Source)
            .IsRequired()
            .HasMaxLength(2048);

        // ── CloudEvent optional context attributes ────────────────────────────
        builder.Property(m => m.Subject)
            .HasMaxLength(1024)
            .IsRequired(false);

        builder.Property(m => m.EventTime)
            .HasColumnName("Time")
            .IsRequired(false);

        builder.Property(m => m.DataContentType)
            .HasMaxLength(256)
            .IsRequired(false);

        builder.Property(m => m.DataSchema)
            .HasMaxLength(2048)
            .IsRequired(false);

        // ── CloudEvent data payload ───────────────────────────────────────────
        builder.Property(m => m.DataText)
            .IsRequired(false);

        builder.Property(m => m.DataBytes)
            .IsRequired(false);

        // ── Outbox delivery-tracking columns ──────────────────────────────────
        builder.Property(m => m.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(m => m.ErrorMessage)
            .IsRequired(false);

        builder.Property(m => m.RetryCount)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(m => m.NextRetryAt)
            .IsRequired(false);

        builder.Property(m => m.CreatedAt)
            .IsRequired();

        builder.Property(m => m.LastStatusAt)
            .IsRequired(false);

        // ── Indexes for the relay processor ───────────────────────────────────
        // The relay queries for pending messages ordered by creation time, so a
        // composite index on (Status, NextRetryAt) speeds up that hot path.
        builder.HasIndex(m => new { m.Status, m.NextRetryAt })
            .HasDatabaseName("IX_OutboxMessages_Status_NextRetryAt");

        // ── One-to-many: extension attributes ────────────────────────────────
        builder.HasMany(m => m.Attributes)
            .WithOne(a => a.Message)
            .HasForeignKey(a => a.MessageId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}


