using CloudNative.CloudEvents;
using Hermodr;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = ResolveSqliteConnectionString(
    builder.Configuration.GetConnectionString("DeadLetter") ?? "Data Source=../shared/deadletters.db",
    AppContext.BaseDirectory);
var source = new Uri(builder.Configuration["Sample:Source"] ?? "https://samples.deveel.events/deadletter/relay/worker");
var dataSchemaBaseUri = new Uri(builder.Configuration["Sample:DataSchemaBaseUri"] ?? "https://samples.deveel.events/schema/");
var replayIntervalSeconds = builder.Configuration.GetValue("Sample:ReplayIntervalSeconds", 5);

builder.Services
    .AddEventPublisher(options =>
    {
        options.Source = source;
        options.DataSchemaBaseUri = dataSchemaBaseUri;
        options.ThrowOnErrors = true;
    })
    .AddChannel<RecoveryChannel>(channelName: "recovery")
    .AddDeadLetter()
    .WithEntityFramework(options => options.UseSqlite(connectionString))
    .WithReplayWorker(options =>
    {
        options.Interval = TimeSpan.FromSeconds(replayIntervalSeconds);
        options.MaxBatchSize = 10;
    });

using var host = builder.Build();

await EnsureDatabaseAsync(host.Services);

Console.WriteLine($"Watching shared dead-letter database: {connectionString}");
Console.WriteLine("The worker will replay pending messages as they appear.");

await host.RunAsync();

static async Task EnsureDatabaseAsync(IServiceProvider services)
{
    await using var scope = services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<DeadLetterDbContext>();
    await dbContext.Database.EnsureCreatedAsync();
}

static string ResolveSqliteConnectionString(string? configuredConnectionString, string baseDirectory)
{
    var builder = new SqliteConnectionStringBuilder(configuredConnectionString ?? "Data Source=deadletters.db");
    var dataSource = builder.DataSource;
    var projectDirectory = Path.GetFullPath(Path.Combine(baseDirectory, "..", "..", ".."));

    if (!Path.IsPathRooted(dataSource))
        dataSource = Path.GetFullPath(Path.Combine(projectDirectory, dataSource));

    var directory = Path.GetDirectoryName(dataSource);
    if (!String.IsNullOrWhiteSpace(directory))
        Directory.CreateDirectory(directory);

    builder.DataSource = dataSource;
    return builder.ToString();
}

internal sealed class RecoveryChannel : IEventPublishChannel
{
    public Task PublishAsync(CloudEvent @event, EventPublishOptions? options = null, CancellationToken cancellationToken = default)
    {
        Console.WriteLine(
            $"Replayed '{@event.Type}' with CloudEvent Id '{@event.Id}' " +
            $"from source '{@event.Source}'.");

        if (@event.Data is string data)
            Console.WriteLine($"Stored payload: {data}");

        return Task.CompletedTask;
    }
}
