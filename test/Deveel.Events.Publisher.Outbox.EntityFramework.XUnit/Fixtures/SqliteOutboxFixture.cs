//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Collections.Concurrent;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Deveel.Events;

/// <summary>
/// xUnit class fixture that opens a shared SQLite in-memory connection and wires the
/// full production DI pipeline — <c>AddEventPublisher().AddOutbox&lt;DbOutboxMessage&gt;().WithEntityFramework(UseSqlite)</c>
/// — exactly as a real application would.  <see cref="OutboxDbContext"/> is resolved
/// from this DI container instead of being constructed manually.
/// </summary>
/// <remarks>
/// <para>
/// A single <see cref="SqliteConnection"/> is kept open for the fixture lifetime so that
/// all contexts resolved from the DI container share the same in-memory database.
/// </para>
/// <para>
/// <see cref="WithEntityFramework"/> registers <see cref="OutboxDbContext"/> as
/// <see cref="ServiceLifetime.Scoped"/>.  <see cref="CreateContext"/> therefore creates a
/// fresh <see cref="IServiceScope"/> for each call, returning the context owned by that
/// scope.  All outstanding scopes are disposed when the fixture is torn down.
/// </para>
/// </remarks>
public sealed class SqliteOutboxFixture : IAsyncLifetime
{
    private readonly SqliteConnection _connection;

    // Built in InitializeAsync once the connection is open.
    private ServiceProvider _serviceProvider = null!;

    // Tracks every scope created by CreateContext() so they are all disposed on teardown.
    private readonly ConcurrentBag<IServiceScope> _scopes = [];

    public SqliteOutboxFixture()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
    }

    // ── Public helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a new <see cref="IServiceScope"/>, resolves <see cref="OutboxDbContext"/>
    /// from it, and returns the context.  The scope is tracked internally and disposed
    /// when the fixture is torn down.  The caller is responsible for disposing the context
    /// itself (the typical <c>await using var ctx = …</c> pattern).
    /// </summary>
    public OutboxDbContext CreateContext()
    {
        var scope = _serviceProvider.CreateScope();
        _scopes.Add(scope);
        return scope.ServiceProvider.GetRequiredService<OutboxDbContext>();
    }

    // ── IAsyncLifetime ────────────────────────────────────────────────────────

    public async ValueTask InitializeAsync()
    {
        await _connection.OpenAsync();

        // Use the same DI registration flow a production application would use.
        // WithEntityFramework registers OutboxDbContext via AddDbContext (Scoped by default).
        var services = new ServiceCollection();
        services.AddEventPublisher()
            .AddOutbox<DbOutboxMessage>()
            .WithEntityFramework(options => options.UseSqlite(_connection));

        _serviceProvider = services.BuildServiceProvider();

        // Create the outbox schema once using a scoped context, then let the scope dispose.
        using var scope = _serviceProvider.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<OutboxDbContext>();
        await ctx.Database.EnsureCreatedAsync();
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var scope in _scopes)
            scope.Dispose();

        await _serviceProvider.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
