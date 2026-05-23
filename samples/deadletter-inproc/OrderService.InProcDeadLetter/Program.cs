using CloudNative.CloudEvents;
using Hermodr;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrderService.InProcDeadLetter.Events;

var builder = Host.CreateApplicationBuilder(args);

var source = new Uri(builder.Configuration["Sample:Source"] ?? "https://samples.deveel.events/deadletter/inproc");
var dataSchemaBaseUri = new Uri(builder.Configuration["Sample:DataSchemaBaseUri"] ?? "https://samples.deveel.events/schema/");
var initialChannel = builder.Configuration["Sample:InitialChannel"] ?? "primary";
var replayChannel = builder.Configuration["Sample:ReplayChannel"] ?? "recovery";

builder.Services
    .AddEventPublisher(options =>
    {
        options.Source = source;
        options.DataSchemaBaseUri = dataSchemaBaseUri;
        options.ThrowOnErrors = true;
    })
    .AddChannel<FailingPrimaryChannel>(channelName: initialChannel)
    .AddChannel<RecoveryChannel>(channelName: replayChannel)
    .AddDeadLetter(deadLetter => deadLetter.UseHandler(async context =>
    {
        Console.WriteLine(
            $"Dead-letter captured for '{context.Event.Type}' from channel '{context.ChannelName}'. " +
            $"Replaying through the pipeline to '{replayChannel}'.");

        var publisher = context.Services.GetRequiredService<IEventPublisher>();
        await publisher.PublishEventAsync(
            context.Event,
            new DeadLetterReplayPublishOptions(new NamedChannelPublishOptions(replayChannel)),
            context.CancellationToken);
    }));

using var host = builder.Build();

var publisher = host.Services.GetRequiredService<IEventPublisher>();
var orderEvent = new OrderSubmitted(
    OrderId: Guid.NewGuid().ToString("N"),
    CustomerId: "customer-42",
    TotalAmount: 149.90m);

Console.WriteLine($"Publishing order '{orderEvent.OrderId}' to channel '{initialChannel}'...");

try
{
    await publisher.PublishAsync(orderEvent, initialChannel);
}
catch (EventPublishException ex)
{
    Console.WriteLine($"The original publish still fails fast: {ex.Message}");
}

Console.WriteLine("Sample completed.");

internal sealed class FailingPrimaryChannel : IEventPublishChannel
{
    public Task PublishAsync(CloudEvent @event, EventPublishOptions? options = null, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"Primary channel received '{@event.Type}' and simulates a transport outage.");
        throw new InvalidOperationException("The primary transport is unavailable.");
    }
}

internal sealed class RecoveryChannel : IEventPublishChannel
{
    public Task PublishAsync(CloudEvent @event, EventPublishOptions? options = null, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"Recovery channel accepted '{@event.Type}' with CloudEvent Id '{@event.Id}'.");

        if (@event.Data is OrderSubmitted order)
        {
            Console.WriteLine(
                $"Replayed order '{order.OrderId}' for customer '{order.CustomerId}' " +
                $"with total {order.TotalAmount:C}.");
        }

        return Task.CompletedTask;
    }
}
