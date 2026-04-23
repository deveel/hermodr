# Publish Channels

A **publish channel** is responsible for taking a `CloudEvent` and delivering it to a specific transport (Azure Service Bus, RabbitMQ, HTTP, …).

## Core interfaces

### `IEventPublishChannel`

```csharp
public interface IEventPublishChannel
{
    Task PublishAsync(CloudEvent @event, CancellationToken cancellationToken = default);
}
```

The `EventPublisher` resolves **all** registered `IEventPublishChannel` services and calls `PublishAsync` on each one in sequence for every outgoing event.

### `IEventPublishChannel<TOptions>`

An optional generic interface for channels that accept per-call delivery options (e.g. supplying a dynamic webhook URL):

```csharp
public interface IEventPublishChannel<TOptions> : IEventPublishChannel
{
    Task PublishAsync(CloudEvent @event, TOptions options, CancellationToken cancellationToken = default);
}
```

### `IBatchEventPublishChannel<TOptions>`

Extends the above to support publishing multiple events in a single call:

```csharp
public interface IBatchEventPublishChannel<TOptions>
{
    Task PublishBatchAsync(
        IEnumerable<CloudEvent> events,
        TOptions options,
        CancellationToken cancellationToken = default);
}
```

The Webhook channel implements both `IEventPublishChannel<WebhookPublishOptions>` and `IBatchEventPublishChannel<WebhookPublishOptions>`.

## Built-in channels

| Channel | Package | Registration method |
|---------|---------|---------------------|
| Azure Service Bus | `Deveel.Events.Publisher.AzureServiceBus` | `.AddServiceBusChannel(...)` |
| RabbitMQ | `Deveel.Events.Publisher.RabbitMq` | `.UseRabbitMq(...)` |
| MassTransit | `Deveel.Events.Publisher.MassTransit` | `.UseMassTransit(...)` |
| Webhook (HTTP) | `Deveel.Events.Publisher.Webhook` | `.UseWebhook(...)` |
| Test (in-memory) | `Deveel.Events.TestPublisher` | `.AddTestChannel(...)` |

Multiple channels can be registered simultaneously — the publisher will deliver to all of them.

## Implementing a custom channel

To create your own channel, implement `IEventPublishChannel` and register it in DI:

```csharp
using CloudNative.CloudEvents;
using Deveel.Events;

public class KafkaEventPublishChannel : IEventPublishChannel
{
    private readonly KafkaProducerOptions _options;

    public KafkaEventPublishChannel(IOptions<KafkaProducerOptions> options)
    {
        _options = options.Value;
    }

    public async Task PublishAsync(CloudEvent @event, CancellationToken cancellationToken = default)
    {
        // ... serialise and send via Confluent.Kafka
    }
}
```

Register it with the builder:

```csharp
builder.Services
    .AddEventPublisher()
    .Services  // IServiceCollection
        .AddSingleton<IEventPublishChannel, KafkaEventPublishChannel>();
```

Or use the builder's `Services` property:

```csharp
var publisherBuilder = builder.Services.AddEventPublisher();
publisherBuilder.Services.AddSingleton<IEventPublishChannel, KafkaEventPublishChannel>();
```

## Related pages

- [Azure Service Bus](../publishers/azure-service-bus.md)
- [RabbitMQ](../publishers/rabbitmq.md)
- [MassTransit](../publishers/masstransit.md)
- [Webhook](../publishers/webhook.md)
- [Test Publisher](../testing/README.md)

