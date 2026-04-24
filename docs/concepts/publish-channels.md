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

An optional generic interface for channels that accept per-call delivery options (e.g. supplying a dynamic webhook URL or routing key):

```csharp
public interface IEventPublishChannel<TOptions> : IEventPublishChannel
{
    Task PublishAsync(CloudEvent @event, TOptions options, CancellationToken cancellationToken = default);
}
```

All built-in channels implement this interface.  When `options` is `null`, the channel-level defaults are used as-is.  When non-`null`, each channel performs a **property-level merge** rather than a wholesale replacement:

- **Nullable reference-type properties** (`string?`, `Uri?`, `JsonSerializerOptions?`, …): a `null` value in the per-call options means *"leave this field at the channel default"*; a non-`null` value overrides it.
- **Nullable value-type properties** (`bool?`, `TimeSpan?`, enum `?`, …): same rule — `null` inherits the channel default, any explicit value overrides it.
- **Non-nullable string properties** (`ConnectionString`, `QueueName` in the Service Bus channel): an empty or whitespace value falls back to the channel default; a non-empty value overrides it.
- **Channel-structural fields** that are not meaningful to override per-call (e.g. `SignatureHeaderName`, `RetryableStatusCodes` in the Webhook channel) are always taken from the channel-level defaults and ignored in per-call overrides.

This design lets callers override only what they need — for example, changing the routing key or destination address for one delivery — without having to repeat every setting from the channel configuration.

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

