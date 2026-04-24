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
    .UseMassTransit(options =>
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
    .UseMassTransit();
```

### From `appsettings.json`

```csharp
builder.Services
    .AddEventPublisher()
    .UseMassTransit("Events:MassTransit");
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

`MassTransitEventPublishOptions`

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

## Per-delivery options

Every channel registered with `UseMassTransit` also implements `IEventPublishChannel<MassTransitEventPublishOptions>`, letting you override individual properties for a single publish call.  Only the properties you set (non-`null`) replace the channel default — all others fall back to the values configured at registration time.

```csharp
// Resolve the typed channel from DI
var channel = serviceProvider
    .GetRequiredService<IEventPublishChannel<MassTransitEventPublishOptions>>();

// Send this event directly to a specific queue,
// while still inheriting MapAttributesToHeaders from the channel default.
await channel.PublishAsync(@event, new MassTransitEventPublishOptions
{
    DestinationAddress = new Uri("queue:priority-orders"),
});
```

## Consuming CloudEvents with MassTransit

On the consumer side, implement a MassTransit consumer for `ICloudEventMessage`:

```csharp
using MassTransit;
using Deveel.Events;

public class OrderPlacedConsumer : IConsumer<ICloudEventMessage>
{
    public Task Consume(ConsumeContext<ICloudEventMessage> context)
    {
        var cloudEvent = context.Message.CloudEvent;
        Console.WriteLine($"Received: {cloudEvent.Type} ({cloudEvent.Id})");
        return Task.CompletedTask;
    }
}
```

Register the consumer with MassTransit as you normally would.

## Related pages

- [Publisher Channels Overview](README.md)
- [Event Publisher](../concepts/event-publisher.md)

