using System.Collections.Concurrent;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Hermodr;

public sealed class SqliteAuditTrailFixture : IAsyncLifetime
{
    private readonly SqliteConnection _connection;
    private ServiceProvider _serviceProvider = null!;
    private readonly ConcurrentBag<IServiceScope> _scopes = [];

    public SqliteAuditTrailFixture()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
    }

    public AuditTrailDbContext CreateContext()
    {
        var scope = _serviceProvider.CreateScope();
        _scopes.Add(scope);
        return scope.ServiceProvider.GetRequiredService<AuditTrailDbContext>();
    }

    public EntityAuditTrail CreateAuditTrail()
    {
        var scope = _serviceProvider.CreateScope();
        _scopes.Add(scope);
        return scope.ServiceProvider.GetRequiredService<EntityAuditTrail>();
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
            .AddAuditTrail(audit => audit.UseEntityFramework(options =>
                options.UseSqlite(_connection)));

        _serviceProvider = services.BuildServiceProvider();

        using var scope = _serviceProvider.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AuditTrailDbContext>();
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
