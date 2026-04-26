# Publish Channels

A **publish channel** is responsible for taking a `CloudEvent` and delivering it to a specific transport (Azure Service Bus, RabbitMQ, HTTP, …).

## Core interfaces

### `IEventPublishChannel`

```csharp
public interface IEventPublishChannel
{
    Task PublishAsync(CloudEvent @event, EventPublishOptions? options = null, CancellationToken cancellationToken = default);
}
```

The `EventPublisher` resolves **all** registered `IEventPublishChannel` services and calls `PublishAsync` on each one in sequence for every outgoing event.

The optional `options` parameter accepts any `EventPublishOptions` subclass and allows per-call delivery overrides.  All built-in channels extend `EventPublishChannel<TOptions>`, which performs a **property-level merge** of the per-call overrides with the channel-level defaults before delivery:

- **Nullable reference-type properties** (`string?`, `Uri?`, `JsonSerializerOptions?`, …): a `null` value in the per-call options means *"leave this field at the channel default"*; a non-`null` value overrides it.
- **Nullable value-type properties** (`bool?`, `TimeSpan?`, enum `?`, …): same rule — `null` inherits the channel default, any explicit value overrides it.
- **Non-nullable string properties** (`ConnectionString`, `QueueName` in the Service Bus channel): an empty or whitespace value falls back to the channel default; a non-empty value overrides it.
- **Channel-structural fields** that are not meaningful to override per-call (e.g. `SignatureHeaderName`, `RetryableStatusCodes` in the Webhook channel) are always taken from the channel-level defaults and ignored in per-call overrides.

This design lets callers override only what they need — for example, changing the routing key or destination address for one delivery — without having to repeat every setting from the channel configuration.

### `INamedEventPublishChannel`

An optional interface that a channel implements to expose a **logical name**:

```csharp
public interface INamedEventPublishChannel : IEventPublishChannel
{
    string? Name { get; }
}
```

Channels that do **not** implement `INamedEventPublishChannel` are treated as **anonymous** and always receive every event, regardless of any name filter supplied at publish time.

`EventPublishChannel<TOptions>` implements `INamedEventPublishChannel` automatically: it reads `Name` from the channel-level options when those options implement `INamedChannelFilter` — you do not need to add anything to the channel class itself.

See [Named Channels](../publishers/named-channels.md) for the full guide on registering and targeting named channel instances.

### `IEventPublishChannel<TEvent>`

A generic marker interface for channels keyed against a specific **annotated event class**.
`EventPublisher` resolves `IEventPublishChannel<TEvent>` to find channels registered for a particular event class and routes the event exclusively to those channels instead of (or in addition to) the general-purpose ones.

```csharp
public interface IEventPublishChannel<TEvent> : IEventPublishChannel
    where TEvent : class
{
}
```

> ⚠️ The type argument `TEvent` must be the **event event class** (e.g. `OrderPlaced`), not a channel options type.  Per-call delivery overrides are supplied as a typed `EventPublishOptions` subclass instance passed directly to `PublishAsync` — they do not require a separate typed channel registration.

### `IBatchEventPublishChannel`

Extends `IEventPublishChannel` to support publishing multiple events in a single call:

```csharp
public interface IBatchEventPublishChannel : IEventPublishChannel
{
    Task PublishBatchAsync(
        IReadOnlyList<CloudEvent> events,
        EventPublishOptions? options = null,
        CancellationToken cancellationToken = default);
}
```

The Webhook channel implements both `IEventPublishChannel` and `IBatchEventPublishChannel`.

## Built-in channels


| Channel | Package | Registration method | Typed overload |
|---------|---------|---------------------|----------------|
| Azure Service Bus | `Deveel.Events.Publisher.AzureServiceBus` | `.AddServiceBus(...)` | `.AddServiceBus<TEvent>(...)` |
| RabbitMQ | `Deveel.Events.Publisher.RabbitMq` | `.AddRabbitMq(...)` | `.AddRabbitMq<TEvent>(...)` |
| MassTransit | `Deveel.Events.Publisher.MassTransit` | `.AddMassTransit(...)` | `.AddMassTransit<TEvent>(...)` |
| Webhook (HTTP) | `Deveel.Events.Publisher.Webhook` | `.AddWebhooks(...)` | `.AddWebhooks<TEvent>(...)` |
| Test (in-memory) | `Deveel.Events.TestPublisher` | `.AddTestChannel(...)` | — |

Multiple channels can be registered simultaneously — the publisher will deliver to all of them.  When a typed channel exists for `TEvent`, **only typed channels** receive that event.

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

    public async Task PublishAsync(CloudEvent @event, EventPublishOptions? options = null, CancellationToken cancellationToken = default)
    {
        // ... serialise and send via Confluent.Kafka
    }
}
```

Register it with the builder's `AddChannel<TChannel>()` helper (which calls `TryAddSingleton` for the concrete type and `AddSingleton<IEventPublishChannel, TChannel>()`):

```csharp
builder.Services
    .AddEventPublisher()
    .AddChannel<KafkaEventPublishChannel>();
```

Or register it directly on the `IServiceCollection`:

```csharp
var publisherBuilder = builder.Services.AddEventPublisher();
publisherBuilder.Services.AddSingleton<IEventPublishChannel, KafkaEventPublishChannel>();
```

### Custom typed channel

To restrict a channel to a single event class, implement `IEventPublishChannel<TEvent>` and use `AddChannel<TChannel, TEvent>()`:

```csharp
public class KafkaOrderChannel : IEventPublishChannel<OrderPlaced>
{
    public async Task PublishAsync(CloudEvent @event, EventPublishOptions? options = null, CancellationToken cancellationToken = default)
    {
        // ... send only OrderPlaced events to Kafka
    }
}
```

```csharp
builder.Services
    .AddEventPublisher()
    .AddChannel<KafkaOrderChannel, OrderPlaced>();
```

`AddChannel<TChannel, TEvent>()` registers the concrete type and exposes it as both `IEventPublishChannel` and `IEventPublishChannel<OrderPlaced>`.

### Custom named channel

To participate in name-based routing, implement `INamedEventPublishChannel` alongside `IEventPublishChannel`:

```csharp
public class KafkaEventPublishChannel : IEventPublishChannel, INamedEventPublishChannel
{
    private readonly KafkaChannelOptions _options;

    public KafkaEventPublishChannel(IOptions<KafkaChannelOptions> options)
        => _options = options.Value;

    public string? Name => _options.ChannelName;

    public async Task PublishAsync(CloudEvent @event, EventPublishOptions? options = null, CancellationToken cancellationToken = default)
    {
        // ... deliver via Confluent.Kafka
    }
}
```

Channels that extend `EventPublishChannel<TOptions>` with options that implement `INamedChannelFilter` receive `INamedEventPublishChannel` support automatically — no extra code needed.

## Related pages

- [Named Channels](../publishers/named-channels.md)
- [Typed Channels](../publishers/typed-channels.md)
- [Azure Service Bus](../publishers/azure-service-bus.md)
- [RabbitMQ](../publishers/rabbitmq.md)
- [MassTransit](../publishers/masstransit.md)
- [Webhook](../publishers/webhook.md)
- [Test Publisher](../testing/README.md)


```csharp
public interface IEventPublishChannel
{
    Task PublishAsync(CloudEvent @event, EventPublishOptions? options = null, CancellationToken cancellationToken = default);
}
```

The `EventPublisher` resolves **all** registered `IEventPublishChannel` services and calls `PublishAsync` on each one in sequence for every outgoing event.

The optional `options` parameter accepts any `EventPublishOptions` subclass and allows per-call delivery overrides.  All built-in channels extend `EventPublishChannel<TOptions>`, which performs a **property-level merge** of the per-call overrides with the channel-level defaults before delivery:

- **Nullable reference-type properties** (`string?`, `Uri?`, `JsonSerializerOptions?`, …): a `null` value in the per-call options means *"leave this field at the channel default"*; a non-`null` value overrides it.
- **Nullable value-type properties** (`bool?`, `TimeSpan?`, enum `?`, …): same rule — `null` inherits the channel default, any explicit value overrides it.
- **Non-nullable string properties** (`ConnectionString`, `QueueName` in the Service Bus channel): an empty or whitespace value falls back to the channel default; a non-empty value overrides it.
- **Channel-structural fields** that are not meaningful to override per-call (e.g. `SignatureHeaderName`, `RetryableStatusCodes` in the Webhook channel) are always taken from the channel-level defaults and ignored in per-call overrides.

This design lets callers override only what they need — for example, changing the routing key or destination address for one delivery — without having to repeat every setting from the channel configuration.

### `IEventPublishChannel<TEvent>`

A generic marker interface for channels keyed against a specific **annotated event class**.
`EventPublisher` resolves `IEventPublishChannel<TEvent>` to find channels registered for a particular event class and routes the event exclusively to those channels instead of (or in addition to) the general-purpose ones.

```csharp
public interface IEventPublishChannel<TEvent> : IEventPublishChannel
    where TEvent : class
{
}
```

> ⚠️ The type argument `TEvent` must be the **event class** (e.g. `OrderPlaced`), not a channel options type.  Per-call delivery overrides are supplied as a typed `EventPublishOptions` subclass instance passed directly to `PublishAsync` — they do not require a separate typed channel registration.

### `IBatchEventPublishChannel`

Extends `IEventPublishChannel` to support publishing multiple events in a single call:

```csharp
public interface IBatchEventPublishChannel : IEventPublishChannel
{
    Task PublishBatchAsync(
        IReadOnlyList<CloudEvent> events,
        EventPublishOptions? options = null,
        CancellationToken cancellationToken = default);
}
```

The Webhook channel implements both `IEventPublishChannel` and `IBatchEventPublishChannel`.

## Built-in channels


| Channel | Package | Registration method | Typed overload |
|---------|---------|---------------------|----------------|
| Azure Service Bus | `Deveel.Events.Publisher.AzureServiceBus` | `.AddServiceBus(...)` | `.AddServiceBus<TEvent>(...)` |
| RabbitMQ | `Deveel.Events.Publisher.RabbitMq` | `.AddRabbitMq(...)` | `.AddRabbitMq<TEvent>(...)` |
| MassTransit | `Deveel.Events.Publisher.MassTransit` | `.AddMassTransit(...)` | `.AddMassTransit<TEvent>(...)` |
| Webhook (HTTP) | `Deveel.Events.Publisher.Webhook` | `.AddWebhooks(...)` | `.AddWebhooks<TEvent>(...)` |
| Test (in-memory) | `Deveel.Events.TestPublisher` | `.AddTestChannel(...)` | — |

Multiple channels can be registered simultaneously — the publisher will deliver to all of them.  When a typed channel exists for `TEvent`, **only typed channels** receive that event.

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

    public async Task PublishAsync(CloudEvent @event, EventPublishOptions? options = null, CancellationToken cancellationToken = default)
    {
        // ... serialise and send via Confluent.Kafka
    }
}
```

Register it with the builder's `AddChannel<TChannel>()` helper (which calls `TryAddSingleton` for the concrete type and `AddSingleton<IEventPublishChannel, TChannel>()`):

```csharp
builder.Services
    .AddEventPublisher()
    .AddChannel<KafkaEventPublishChannel>();
```

Or register it directly on the `IServiceCollection`:

```csharp
var publisherBuilder = builder.Services.AddEventPublisher();
publisherBuilder.Services.AddSingleton<IEventPublishChannel, KafkaEventPublishChannel>();
```

### Custom typed channel

To restrict a channel to a single event class, implement `IEventPublishChannel<TEvent>` and use `AddChannel<TChannel, TEvent>()`:

```csharp
public class KafkaOrderChannel : IEventPublishChannel<OrderPlaced>
{
    public async Task PublishAsync(CloudEvent @event, EventPublishOptions? options = null, CancellationToken cancellationToken = default)
    {
        // ... send only OrderPlaced events to Kafka
    }
}
```

```csharp
builder.Services
    .AddEventPublisher()
    .AddChannel<KafkaOrderChannel, OrderPlaced>();
```

`AddChannel<TChannel, TEvent>()` registers the concrete type and exposes it as both `IEventPublishChannel` and `IEventPublishChannel<OrderPlaced>`.

## Related pages

- [Typed Channels](../publishers/typed-channels.md)
- [Azure Service Bus](../publishers/azure-service-bus.md)
- [RabbitMQ](../publishers/rabbitmq.md)
- [MassTransit](../publishers/masstransit.md)
- [Webhook](../publishers/webhook.md)
- [Test Publisher](../testing/README.md)

