# Typed Publish Channels

A **typed channel** is a channel that handles only events whose data class matches a specific type.  It lets you configure separate delivery settings — queue name, endpoint URL, exchange, etc. — for each domain event without duplicating the plumbing of the underlying transport.

## Motivation

By default every registered channel receives **all** events.  When you need event-type-specific routing (different queue per event, different endpoint URL per domain type, different signing secret, …) you have two options:

1. **Per-call overrides** — pass a `EventPublishOptions` subclass to every `PublishAsync` call. Simple, but invasive; the publishing code must know the routing details.
2. **Typed channels** — configure a separate channel instance at startup; the `EventPublisher` routes automatically. The publishing code stays free of routing concerns.

## How routing works

When `EventPublisher.PublishAsync` is called for an event whose data class is `TEvent`, the publisher:

1. Looks up all services registered as `IEventPublishChannel<TEvent>`.
2. If any are found, it delivers the event **exclusively** to those typed channels (the general `IEventPublishChannel` instances are skipped for that event).
3. If none are found, it falls back to all general `IEventPublishChannel` registrations.

This means typed channels **take priority** over general ones for matching events.

> **Note:** the `TEvent` type argument is the **event data class** (e.g. `OrderPlacedData`), not a channel options type or a `CloudEvent`.

## Two-level options hierarchy

Every built-in typed channel subclass inherits from its non-typed counterpart and implements `IEventPublishChannel<TEvent>`.  At construction time it **merges** the general channel options with the type-specific ones:

```
IOptions<TOptions>                   ← registered by AddXxx(configure)
       +
IOptions<TOptions<TEvent>>           ← registered by AddXxx<TEvent>(configure)
       ↓
TOptions.Merge(baseOptions, typedOptions)
       ↓
merged options passed to the parent channel
```

**Merge rules**

| Property kind | Wins |
|---------------|------|
| Nullable reference-type (`string?`, `Uri?`, …) | Typed if non-`null`; base otherwise |
| Nullable value-type (`bool?`, `TimeSpan?`, enum `?`) | Typed if non-`null`; base otherwise |
| Non-nullable string (Service Bus `ConnectionString`, `QueueName`) | Typed if non-empty/whitespace; base otherwise |
| Channel-structural (header names, retry codes, …) | **Always** from base |
| `AdditionalHeaders` (Webhook) | Merged; typed entries win on key collision |

This means you can register a general channel with shared defaults and then specialize individual event types by overriding only the properties that differ.

## Registration

### RabbitMQ

```csharp
builder.Services
    .AddEventPublisher()
    // Shared defaults for all RabbitMQ traffic
    .AddRabbitMq(opts =>
    {
        opts.ConnectionString = "amqp://guest:guest@localhost:5672";
        opts.ExchangeName     = "events";
        opts.PersistentMessages = true;
        opts.PublisherConfirms   = true;
    })
    // OrderPlaced events go to a dedicated exchange and queue
    .AddRabbitMq<OrderPlaced>(opts =>
    {
        opts.ExchangeName = "orders";
        opts.QueueName    = "order-placed";
        opts.RoutingKey   = "order.placed";
    });
```

The typed channel binds `IOptions<RabbitMqPublishOptions<OrderPlaced>>` and merges it with the base `IOptions<RabbitMqPublishOptions>` at construction time.  `ConnectionString`, `PersistentMessages`, and `PublisherConfirms` are inherited from the base; `ExchangeName`, `QueueName`, and `RoutingKey` are overridden by the typed options.

From configuration:

```csharp
builder.Services
    .AddEventPublisher()
    .AddRabbitMq("Events:RabbitMq")
    .AddRabbitMq<OrderPlaced>("Events:RabbitMq:Orders");
```

```json
{
  "Events": {
    "RabbitMq": {
      "ConnectionString": "amqp://guest:guest@localhost:5672",
      "ExchangeName": "events",
      "Orders": {
        "ExchangeName": "orders",
        "QueueName": "order-placed",
        "RoutingKey": "order.placed"
      }
    }
  }
}
```

### Azure Service Bus

```csharp
builder.Services
    .AddEventPublisher()
    // General channel — all untyped events go here
    .AddServiceBus(opts =>
    {
        opts.ConnectionString = "<connection-string>";
        opts.QueueName        = "events";
    })
    // OrderPlaced events go to a dedicated queue
    .AddServiceBus<OrderPlaced>(opts =>
    {
        opts.QueueName = "order-placed";
        // ConnectionString inherited from the base options
    });
```

From configuration:

```csharp
builder.Services
    .AddEventPublisher()
    .AddServiceBus("Events:ServiceBus")
    .AddServiceBus<OrderPlaced>("Events:ServiceBus:Orders");
```

> **Note:** `ServiceBusPublishOptions<TEvent>` re-declares `ConnectionString` and `QueueName` as _nullable_ (`string?`) so that leaving them unset is the unambiguous signal to "inherit from the base channel".  The non-nullable constraint is enforced on the _merged_ result only.

### MassTransit

```csharp
builder.Services
    .AddEventPublisher()
    // Default: publish (fan-out) via IPublishEndpoint
    .AddMassTransit(opts =>
    {
        opts.MapAttributesToHeaders = true;
    })
    // OrderPlaced events are sent to a specific endpoint
    .AddMassTransit<OrderPlaced>(opts =>
    {
        opts.DestinationAddress = new Uri("queue:order-placed");
    });
```

From configuration:

```csharp
builder.Services
    .AddEventPublisher()
    .AddMassTransit("Events:MassTransit")
    .AddMassTransit<OrderPlaced>("Events:MassTransit:Orders");
```

### Webhook

```csharp
builder.Services
    .AddEventPublisher()
    // General catch-all webhook
    .AddWebhooks(opts =>
    {
        opts.EndpointUrl       = "https://partner.example.com/events";
        opts.SigningSecret      = "shared-secret";
        opts.MaxRetryCount      = 3;
        opts.SignatureAlgorithm = WebhookSignatureAlgorithm.HmacSha256;
    })
    // OrderPlaced delivers to a different endpoint with a dedicated secret
    .AddWebhooks<OrderPlaced>(opts =>
    {
        opts.EndpointUrl  = "https://orders.example.com/hooks";
        opts.SigningSecret = "order-secret";
        // MaxRetryCount and SignatureAlgorithm inherited from base
    });
```

`AdditionalHeaders` are **merged**: the base headers are included; typed-specific entries override on key collision.

From configuration:

```csharp
builder.Services
    .AddEventPublisher()
    .AddWebhooks("Events:Webhook")
    .AddWebhooks<OrderPlaced>("Events:Webhook:Orders");
```

## Multiple typed channels for the same event

You can register several typed channels for the same `TEvent` — the publisher delivers to **all** of them:

```csharp
builder.Services
    .AddEventPublisher()
    .AddRabbitMq<OrderPlaced>(opts => { opts.ExchangeName = "orders"; })
    .AddWebhooks<OrderPlaced>(opts => { opts.EndpointUrl = "https://partner.example.com/hooks"; });
```

When `OrderPlacedData` is published, both the RabbitMQ typed channel and the Webhook typed channel receive it.

## Custom typed channel

To build a typed channel from scratch, implement `IEventPublishChannel<TEvent>` and register it with `AddChannel<TChannel, TEvent>()`:

```csharp
public class KafkaOrderChannel : IEventPublishChannel<OrderPlaced>
{
    // ... constructor, PublishAsync implementation
}
```

```csharp
builder.Services
    .AddEventPublisher()
    .AddChannel<KafkaOrderChannel, OrderPlaced>();
```

`AddChannel<TChannel, TEvent>()` registers `KafkaOrderChannel` both as `IEventPublishChannel` (general broadcast fallback) and as `IEventPublishChannel<OrderPlaced>` (typed routing).

## Typed options classes

Each built-in channel exposes a typed options class `TOptions<TEvent>` (e.g. `RabbitMqPublishOptions<OrderPlaced>`) that inherits from the base options class.  It carries no additional properties; its sole purpose is to give DI a distinct key so base and typed options are bound independently.

| Channel | Typed options class |
|---------|---------------------|
| RabbitMQ | `RabbitMqPublishOptions<TEvent>` |
| Azure Service Bus | `ServiceBusPublishOptions<TEvent>` |
| MassTransit | `MassTransitPublishOptions<TEvent>` |
| Webhook | `WebhookPublishOptions<TEvent>` |

You can use these directly to pre-populate DI options outside the builder convenience methods if needed:

```csharp
services.AddOptions<RabbitMqPublishOptions<OrderPlaced>>()
    .Configure(opts =>
    {
        opts.ExchangeName = "orders";
        opts.QueueName    = "order-placed";
    });
```

## Per-call overrides across multiple typed channels

When you publish an event and want to provide per-call options overrides to **more than one** channel at the same time, use `CombinedPublishOptions`.  It bundles several channel-specific options into a single object that the publisher can unwrap.

The publisher automatically routes each bundled entry to the right channel based on whether the options instance is **general** (non-generic) or **typed** (a closed generic type such as `RabbitMqPublishOptions<TEvent>`):

- A non-generic options instance (e.g. `new RabbitMqPublishOptions { … }`) is forwarded only to the **general** channel — typed channels are not affected.
- A typed options instance (e.g. `new RabbitMqPublishOptions<OrderPlaced> { … }`) is forwarded only to the **typed** channel registered for that event type — no other channels are affected.

```csharp
var overrides = new CombinedPublishOptions(
    // → general RabbitMQ channel
    new RabbitMqPublishOptions    { RoutingKey   = "general.priority" },
    // → typed RabbitMQ channel for OrderPlaced only
    new RabbitMqPublishOptions<OrderPlaced> { RoutingKey = "orders.priority" },
    // → general Webhook channel
    new WebhookPublishOptions     { EndpointUrl  = "https://partner.example.com/priority-hook" });

await publisher.PublishEventAsync(@event, overrides);
```

Channels with no matching entry in the bundle fall back to their registered defaults.

See [Per-call publish options](../concepts/event-publisher.md#per-call-publish-options) for the full resolution rules and `CombinedPublishOptions` API reference.

## Related pages

- [Publish Channels — core interfaces](../concepts/publish-channels.md)
- [Event Publisher](../concepts/event-publisher.md)
- [RabbitMQ Channel](rabbitmq.md)
- [Azure Service Bus Channel](azure-service-bus.md)
- [MassTransit Channel](masstransit.md)
- [Webhook Channel](webhook.md)

