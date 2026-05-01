//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Microsoft.EntityFrameworkCore;

namespace Deveel.Events;

/// <summary>
/// A minimal <see cref="DbContext"/> used exclusively by the integration tests
/// in this project.  It registers both <see cref="DbOutboxMessage"/> and
/// <see cref="DbCloudEventAttribute"/> using the production
/// <see cref="DbOutboxMessageConfiguration"/> and
/// <see cref="DbCloudEventAttributeConfiguration"/> type configurations, so
/// that integration tests exercise exactly the same mapping that ships in the
/// library.
/// </summary>
public class OutboxDbContext : DbContext
{
    public OutboxDbContext(DbContextOptions<OutboxDbContext> options)
        : base(options) { }

    public DbSet<DbOutboxMessage>       OutboxMessages          => Set<DbOutboxMessage>();
    public DbSet<DbCloudEventAttribute> OutboxMessageAttributes => Set<DbCloudEventAttribute>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new DbOutboxMessageConfiguration());
        modelBuilder.ApplyConfiguration(new DbCloudEventAttributeConfiguration());
    }
}

