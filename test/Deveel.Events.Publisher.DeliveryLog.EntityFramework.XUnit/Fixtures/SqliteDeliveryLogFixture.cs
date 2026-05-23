using System.Collections.Concurrent;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Deveel.Events;

public sealed class SqliteDeliveryLogFixture : IAsyncLifetime
{
    private readonly SqliteConnection _connection;
    private ServiceProvider _serviceProvider = null!;
    private readonly ConcurrentBag<IServiceScope> _scopes = [];

    public SqliteDeliveryLogFixture()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
    }

    public DeliveryLogDbContext CreateContext()
    {
        var scope = _serviceProvider.CreateScope();
        _scopes.Add(scope);
        return scope.ServiceProvider.GetRequiredService<DeliveryLogDbContext>();
    }

    public EntityEventDeliveryLogRepository CreateRepository()
    {
        var scope = _serviceProvider.CreateScope();
        _scopes.Add(scope);
        return scope.ServiceProvider.GetRequiredService<EntityEventDeliveryLogRepository>();
    }

    public TService GetService<TService>() where TService : notnull
    {
        var scope = _serviceProvider.CreateScope();
        _scopes.Add(scope);
        return scope.ServiceProvider.GetRequiredService<TService>();
    }

    public async ValueTask InitializeAsync()
    {
        await _connection.OpenAsync();

        var services = new ServiceCollection();

        services.AddLogging(builder => builder.ClearProviders());

        services.AddEventPublisher(opts =>
                opts.Source = new Uri("https://example.com"))
            .AddDeliveryLog(log => log.UseEntityFramework(options =>
                options.UseSqlite(_connection)));

        _serviceProvider = services.BuildServiceProvider();

        using var scope = _serviceProvider.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<DeliveryLogDbContext>();
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
