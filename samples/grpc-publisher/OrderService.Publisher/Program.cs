using Hermodr;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using OrderService.Publisher.Events;
using OrderService.Publisher.GrpcSenders;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddEventPublisher(options =>
    {
        options.Source = new Uri("https://samples.deveel.events/grpc-publisher");
        options.DataSchemaBaseUri = new Uri("https://samples.deveel.events/schema/");
        options.ThrowOnErrors = true;
    })
    // Channel and endpoint configuration come from the "Events:Grpc" section
    // of appsettings.json (address, headers, deadline, ...).
    .AddGrpcEventPublisherChannel("Events:Grpc")
    .AddGrpcEventSender<OrderEventsGrpcSender>();

using var host = builder.Build();
await host.StartAsync();

var publisher = host.Services.GetRequiredService<IEventPublisher>();
var eventFactory = host.Services.GetRequiredService<IEventFactory>();
var batchChannel = host.Services.GetRequiredService<IBatchEventPublishChannel>();

Console.WriteLine("=== Hermodr gRPC Publisher Sample ===");
Console.WriteLine();

var orderId = Guid.NewGuid().ToString("N");

// 1. Single events: every publish is a unary RPC to the configured endpoint.
Console.WriteLine($"Publishing lifecycle events for order '{orderId}' (unary RPC)...");
await publisher.PublishAsync(new OrderCreated(orderId, "customer-42", 149.90m));
await publisher.PublishAsync(new OrderConfirmed(orderId, "customer-42"));
await publisher.PublishAsync(new OrderShipped(orderId, "TRK-2024-XYZ"));

Console.WriteLine();
Console.WriteLine("All single events delivered.");
Console.WriteLine();

// 2. Batch: the events are streamed to the endpoint in one client-streaming RPC.
Console.WriteLine("Publishing a batch of events (client-streaming RPC)...");
var batch = new[]
{
    eventFactory.CreateEventFromData(new OrderCreated(orderId, "customer-42", 149.90m)),
    eventFactory.CreateEventFromData(new OrderConfirmed(orderId, "customer-42")),
    eventFactory.CreateEventFromData(new OrderShipped(orderId, "TRK-2024-XYZ")),
};
// The event ID is normally assigned by the publish pipeline; when using the
// event factory directly, set it before delivering the batch to the channel.
foreach (var @event in batch)
    @event.Id = Guid.NewGuid().ToString("N");

await batchChannel.PublishBatchAsync(batch);

Console.WriteLine();
Console.WriteLine("Batch delivered.");
Console.WriteLine();
Console.WriteLine("=== Sample completed: check the receiver console for the received events ===");

await host.StopAsync();
