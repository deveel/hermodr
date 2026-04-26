# MassTransit Channel

The `Deveel.Events.Publisher.MassTransit` package adds a publish channel that routes `CloudEvent` instances through a MassTransit bus, making Deveel Events broker-agnostic — any MassTransit-supported transport (RabbitMQ, Azure Service Bus, Amazon SQS, Kafka, In-Memory, …) can be used transparently.

## Installation

```bash
dotnet add package Deveel.Events.Publisher.MassTransit
```

> **Prerequisite:** MassTransit must already be configured in your application with at least one transport registered.  See the [MassTransit documentation](https://masstransit.io/documentation) for setup details.

## Registration

### Inline configuration

```csharp
using Deveel.Events;

builder.Services
    .AddEventPublisher()
    .AddMassTransit(options =>
    {
        // Leave DestinationAddress null to publish (fan-out).
        // Set it to send to a specific endpoint.
        options.DestinationAddress  = new Uri("queue:order-events");
        options.MapAttributesToHeaders = true;
    });
```

### Minimal registration (no options)

```csharp
builder.Services
    .AddEventPublisher()
    .AddMassTransit();
```

### From `appsettings.json`

```csharp
builder.Services
    .AddEventPublisher()
    .AddMassTransit("Events:MassTransit");
```

```json
// appsettings.json
{
  "Events": {
    "MassTransit": {
      "DestinationAddress": "queue:order-events",
      "MapAttributesToHeaders": true
    }
  }
}
```

## Options reference

`MassTransitPublishOptions`

| Property | Type | Effective default | Description |
|----------|------|-------------------|-------------|
| `DestinationAddress` | `Uri?` | `null` | When `null`, the event is published via `IPublishEndpoint` (fan-out). When set, the event is sent to that specific queue/exchange via `ISendEndpointProvider`. |
| `MapAttributesToHeaders` | `bool?` | `true` | Maps CloudEvent attributes (`id`, `type`, `source`, `time`, `datacontenttype`, …) to MassTransit message headers. `null` → use channel default (effective default: `true`). |

> **Nullable value-type properties** use `null` as the sentinel meaning *"inherit from the channel-level default"*.  Set them in the channel registration to establish the baseline; set them in a per-call override only when you need to deviate from that baseline for a specific delivery.

## How it works

1. The `MassTransitEventPublishChannel` wraps a `CloudEvent` in a `CloudEventMessage` — an `ICloudEventMessage` implementation that MassTransit can serialise.
2. If `DestinationAddress` is set, the message is sent to that endpoint via `ISendEndpointProvider.GetSendEndpoint`.
3. Otherwise, the message is published via `IPublishEndpoint.Publish`, allowing MassTransit's topology to route it.
4. When `MapAttributesToHeaders` is `true` (or `null`, which defaults to `true`), CloudEvent attributes are written to the outgoing message headers so consumers can inspect them without deserialising the body.

## Typed channel

Use `AddMassTransit<TEvent>()` to register a channel that receives **only** events whose data class is `TEvent`.  At construction time the typed channel (`MassTransitEventPublishChannel<TEvent>`) merges the general `MassTransitPublishOptions` with the type-specific `MassTransitPublishOptions<TEvent>`: non-`null` typed values win; `null` values fall back to the base defaults.

```csharp
builder.Services
    .AddEventPublisher()
    // Default: publish (fan-out) with header mapping
    .AddMassTransit(opts =>
    {
        opts.MapAttributesToHeaders = true;
    })
    // OrderPlaced events are sent to a specific endpoint
    .AddMassTransit<OrderPlaced>(opts =>
    {
        opts.DestinationAddress = new Uri("queue:order-placed");
        // MapAttributesToHeaders inherited from base
    });
```

From configuration:

```csharp
builder.Services
    .AddEventPublisher()
    .AddMassTransit("Events:MassTransit")
    .AddMassTransit<OrderPlaced>("Events:MassTransit:Orders");
```

```json
{
  "Events": {
    "MassTransit": {
      "MapAttributesToHeaders": true,
      "Orders": {
        "DestinationAddress": "queue:order-placed"
      }
    }
  }
}
```

See [Typed Channels](typed-channels.md) for the full merge semantics and further examples.

## Per-delivery options

Pass a `MassTransitPublishOptions` instance as the second argument to `PublishAsync` to override individual properties for a single publish call.  Only the properties you set (non-`null`) replace the channel default — all others fall back to the values configured at registration time.

```csharp
// Resolve the concrete channel directly from DI.
var channel = serviceProvider.GetRequiredService<MassTransitEventPublishChannel>();

// Send this event directly to a specific queue,
// while still inheriting MapAttributesToHeaders from the channel default.
await channel.PublishAsync(@event, new MassTransitPublishOptions
{
    DestinationAddress = new Uri("queue:priority-orders"),
});
```

## Related pages

- [Publisher Channels Overview](README.md)
- [Typed Channels](typed-channels.md)
- [Event Publisher](../concepts/event-publisher.md)

