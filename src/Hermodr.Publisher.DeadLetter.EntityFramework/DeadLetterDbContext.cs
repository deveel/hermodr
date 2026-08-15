//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Microsoft.EntityFrameworkCore;

namespace Hermodr;

/// <summary>
/// A minimal <see cref="DbContext"/> for Entity Framework-backed dead-letter storage.
/// </summary>
public class DeadLetterDbContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeadLetterDbContext"/> class.
    /// </summary>
    /// <param name="options">The options for this context.</param>
    public DeadLetterDbContext(DbContextOptions<DeadLetterDbContext> options) : base(options)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DeadLetterDbContext"/> class.
    /// </summary>
    /// <param name="options">The options for this context.</param>
    protected DeadLetterDbContext(DbContextOptions options) : base(options)
    {
    }

    /// <summary>
    /// Gets the set of persisted dead-letter messages.
    /// </summary>
    public virtual DbSet<DbDeadLetterMessage> DeadLetterMessages { get; set; } = null!;

    /// <summary>
    /// Gets the set of persisted dead-letter message attributes.
    /// </summary>
    public virtual DbSet<DbDeadLetterAttribute> DeadLetterMessageAttributes { get; set; } = null!;

    /// <summary>
    /// Builds the model for this context, discovering and applying entity configurations.
    /// </summary>
    /// <param name="modelBuilder">The builder used to construct the model for this context.</param>
    /// <remarks>Applies all entity type configurations discovered in this assembly.</remarks>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DeadLetterDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
