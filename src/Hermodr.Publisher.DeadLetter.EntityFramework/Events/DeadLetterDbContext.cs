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
    public DeadLetterDbContext(DbContextOptions<DeadLetterDbContext> options) : base(options)
    {
    }

    protected DeadLetterDbContext(DbContextOptions options) : base(options)
    {
    }

    public virtual DbSet<DbDeadLetterMessage> DeadLetterMessages { get; set; } = null!;

    public virtual DbSet<DbDeadLetterAttribute> DeadLetterMessageAttributes { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DeadLetterDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
