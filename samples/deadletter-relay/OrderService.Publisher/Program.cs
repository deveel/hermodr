using CloudNative.CloudEvents;
using Deveel;
using Deveel.Events;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrderService.Publisher.Events;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = ResolveSqliteConnectionString(
    builder.Configuration.GetConnectionString("DeadLetter") ?? "Data Source=../shared/deadletters.db",
    AppContext.BaseDirectory);
var source = new Uri(builder.Configuration["Sample:Source"] ?? "https://samples.deveel.events/deadletter/relay/publisher");
var dataSchemaBaseUri = new Uri(builder.Configuration["Sample:DataSchemaBaseUri"] ?? "https://samples.deveel.events/schema/");

builder.Services
    .AddEventPublisher(options =>
    {
        options.Source = source;
        options.DataSchemaBaseUri = dataSchemaBaseUri;
        options.ThrowOnErrors = true;
    })
    .AddChannel<FailingPrimaryChannel>(channelName: "primary")
    .AddDeadLetter()
    .WithEntityFramework(options => options.UseSqlite(connectionString))
    .WithReplay();

using var host = builder.Build();

await EnsureDatabaseAsync(host.Services);

var publisher = host.Services.GetRequiredService<IEventPublisher>();
var orderEvent = new OrderSubmitted(
    OrderId: Guid.NewGuid().ToString("N"),
    CustomerId: "customer-100",
    TotalAmount: 249.00m);

Console.WriteLine($"Using shared dead-letter database: {connectionString}");
Console.WriteLine($"Publishing order '{orderEvent.OrderId}' to the failing 'primary' channel...");

try
{
    await publisher.PublishAsync(orderEvent, "primary");
}
catch (EventPublishException ex)
{
    Console.WriteLine($"The transport failed as expected: {ex.Message}");
}

await using (var scope = host.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<DeadLetterDbContext>();
    var storedMessage = await dbContext.DeadLetterMessages
        .OrderByDescending(x => x.CreatedAt)
        .FirstOrDefaultAsync();

    if (storedMessage != null)
    {
        Console.WriteLine(
            $"Stored dead-letter message '{storedMessage.Id}' with status '{storedMessage.Status}' " +
            $"for event '{storedMessage.EventType}'.");
    }
}

Console.WriteLine("Start OrderService.DeadLetterWorker to replay the stored message.");

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

internal sealed class FailingPrimaryChannel : IEventPublishChannel
{
    public Task PublishAsync(CloudEvent @event, EventPublishOptions? options = null, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"Primary channel received '{@event.Type}' and simulates a broker outage.");
        throw new InvalidOperationException("The publisher transport is unavailable.");
    }
}
