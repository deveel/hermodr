//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Deveel.Data;

using Microsoft.EntityFrameworkCore;

using Testcontainers.MySql;

namespace Deveel.Events;

/// <summary>
/// xUnit class fixture that starts a MySQL container via Testcontainers,
/// creates the outbox schema with <c>EnsureCreated</c>, and exposes factory
/// helpers to build short-lived <see cref="OutboxDbContext"/> instances for
/// use inside individual integration tests.
/// </summary>
/// <remarks>
/// The container is started once per test-class collection and torn down when
/// the last test class in that collection finishes.  Individual tests are
/// responsible for cleaning up the rows they insert (or they can rely on
/// unique IDs so that tests are naturally isolated).
/// </remarks>
public sealed class MySqlDatabaseFixture : IAsyncLifetime
{
    private readonly MySqlContainer _container;

    public MySqlDatabaseFixture()
    {
        _container = new MySqlBuilder()
            .WithImage("mysql:8.0")
            .WithResourceMapping(Array.Empty<byte>(), "/var/lib/mysql-files/gh-issue-1142")
            .Build();
    }

    // ── Public helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a fully configured set of <see cref="DbContextOptions{OutboxDbContext}"/>
    /// that targets the running MySQL container.
    /// </summary>
    public DbContextOptions<OutboxDbContext> CreateOptions()
    {
        return new DbContextOptionsBuilder<OutboxDbContext>()
            .UseMySQL(_container.GetConnectionString())
            .Options;
    }

    /// <summary>
    /// Creates and returns a new <see cref="OutboxDbContext"/> connected to
    /// the running container.  Each call produces an independent context so
    /// tests can verify round-trips without reading from EF's first-level cache.
    /// </summary>
    public OutboxDbContext CreateContext() => new(CreateOptions());

    /// <summary>
    /// Creates a new <see cref="EntityOutboxMessageRepository{TMessage,TContext}"/> backed
    /// by a fresh <see cref="OutboxDbContext"/>.
    /// </summary>
    /// <param name="systemTime">
    /// An optional frozen clock to inject.  Pass a <see cref="TestSystemTime"/> instance in
    /// tests that need predictable timestamps.  When <c>null</c>, the real wall clock is used.
    /// </param>
    public EntityOutboxMessageRepository<DbOutboxMessage, OutboxDbContext> CreateRepository(
        ISystemTime? systemTime = null)
        => new(CreateContext(), systemTime);

    // ── IAsyncLifetime ────────────────────────────────────────────────────────

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        // Create the two tables (OutboxMessages + OutboxMessageAttributes)
        // using the production EF mapping – ensures we test the real schema.
        await using var ctx = CreateContext();
        await ctx.Database.EnsureCreatedAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _container.StopAsync();
        await _container.DisposeAsync();
    }
}




