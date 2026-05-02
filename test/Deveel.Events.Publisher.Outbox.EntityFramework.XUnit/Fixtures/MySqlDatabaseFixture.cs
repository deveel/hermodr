//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Collections.Concurrent;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Testcontainers.MySql;

namespace Deveel.Events;

/// <summary>
/// xUnit class fixture that starts a MySQL container via Testcontainers and wires the
/// full production DI pipeline — <c>AddEventPublisher().AddOutbox&lt;DbOutboxMessage&gt;().WithEntityFramework(UseMySQL)</c>
/// — exactly as a real application would.  <see cref="OutboxDbContext"/> is resolved
/// from this DI container instead of being constructed manually.
/// </summary>
/// <remarks>
/// <para>
/// The container is started once per test-class collection and torn down when the last
/// test class in that collection finishes.  Individual tests are responsible for cleaning
/// up the rows they insert (or they can rely on unique IDs so that tests are naturally
/// isolated).
/// </para>
/// <para>
/// <see cref="WithEntityFramework"/> registers <see cref="OutboxDbContext"/> as
/// <see cref="ServiceLifetime.Scoped"/>.  <see cref="CreateContext"/> therefore creates a
/// fresh <see cref="IServiceScope"/> for each call, returning the context owned by that
/// scope.  All outstanding scopes are disposed when the fixture is torn down.
/// </para>
/// </remarks>
public sealed class MySqlDatabaseFixture : IAsyncLifetime
{
    private readonly MySqlContainer _container;

    // Built in InitializeAsync once the container connection string is available.
    private ServiceProvider _serviceProvider = null!;

    // Tracks every scope created by CreateContext() so they are all disposed on teardown.
    private readonly ConcurrentBag<IServiceScope> _scopes = [];

    public MySqlDatabaseFixture()
    {
        _container = new MySqlBuilder()
            .WithImage("mysql:8.0")
            .Build();
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
        await _container.StartAsync();

        // Use the same DI registration flow a production application would use.
        // The connection string is known only after the container has started.
        var services = new ServiceCollection();
        services.AddEventPublisher()
            .AddOutbox<DbOutboxMessage>()
            .WithEntityFramework(options => options.UseMySQL(_container.GetConnectionString()));

        _serviceProvider = services.BuildServiceProvider();

        // Create the two tables (OutboxMessages + OutboxMessageAttributes)
        // using the production EF mapping – ensures we test the real schema.
        using var scope = _serviceProvider.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<OutboxDbContext>();
        await ctx.Database.EnsureCreatedAsync();
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var scope in _scopes)
            scope.Dispose();

        await _serviceProvider.DisposeAsync();
        await _container.StopAsync();
        await _container.DisposeAsync();
    }
}
