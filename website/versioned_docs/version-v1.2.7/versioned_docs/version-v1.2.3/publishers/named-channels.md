# Named Channels

When multiple channels of the **same transport type** are registered simultaneously — for example two RabbitMQ channels pointing at different brokers, or two Webhook channels delivering to different partners — you need a way to target individual channel instances at publish time without knowing their concrete type.

**Named channels** give each registered channel a logical string name.  You can then address that channel by name in per-call options or through convenience extension methods, independently of the channel type or the number of other channels registered for the same transport.

---

## How naming works

### `INamedChannelFilter` — setting a name on options

The `INamedChannelFilter` interface is implemented by any `EventPublishOptions` subclass that wants to participate in name-based routing:

```csharp
public interface INamedChannelFilter
{
    string? ChannelName { get; set; }
}
```

When set on **channel-level options** at startup, `ChannelName` declares the channel's own logical identity.  When set on **per-call options**, it acts as a filter — only channels whose name matches will receive the event.

All built-in options classes (`RabbitMqPublishOptions`, `ServiceBusPublishOptions`, `MassTransitPublishOptions`, `WebhookPublishOptions`) implement `INamedChannelFilter`.

### `INamedEventPublishChannel` — exposing a name on channels

Channels opt into naming by implementing `INamedEventPublishChannel`:

```csharp
public interface INamedEventPublishChannel : IEventPublishChannel
{
    string? Name { get; }
}
```

Channels that **do not** implement this interface are treated as **anonymous** — they always receive every event regardless of any `ChannelName` filter.

`EventPublishChannel<TOptions>` implements `INamedEventPublishChannel` automatically: it reads `Name` from the channel-level options when those options implement `INamedChannelFilter`.  You do not need to set anything extra on the channel class itself.

---

## Declaring a named channel at startup

Set `ChannelName` in the channel's options during registration:

```csharp
builder.Services
    .AddEventPublisher()
    // First RabbitMQ channel — handles order events
    .AddRabbitMq(opts =>
    {
        opts.ChannelName      = "rabbit-orders";
        opts.ConnectionString = "amqp://guest:guest@broker1:5672";
        opts.ExchangeName     = "orders";
    })
    // Second RabbitMQ channel — handles notification events
    .AddRabbitMq(opts =>
    {
        opts.ChannelName      = "rabbit-notifications";
        opts.ConnectionString = "amqp://guest:guest@broker2:5672";
        opts.ExchangeName     = "notifications";
    });
```

Both channels are registered as `IEventPublishChannel`.  At publish time the publisher checks each channel's `INamedEventPublishChannel.Name` and routes accordingly.

From configuration:

```json
{
  "Events": {
    "RabbitMq": {
      "Orders": {
        "ChannelName": "rabbit-orders",
        "ConnectionString": "amqp://guest:guest@broker1:5672",
        "ExchangeName": "orders"
      },
      "Notifications": {
        "ChannelName": "rabbit-notifications",
        "ConnectionString": "amqp://guest:guest@broker2:5672",
        "ExchangeName": "notifications"
      }
    }
  }
}
```

```csharp
builder.Services
    .AddEventPublisher()
    .AddRabbitMq("Events:RabbitMq:Orders")
    .AddRabbitMq("Events:RabbitMq:Notifications");
```

---

## Publishing to a named channel

### Convenience extension methods

The simplest approach — use the `string channelName` overloads on `EventPublisher`:

```csharp
// Publish an annotated data object to a specific named channel
await publisher.PublishAsync(orderPlaced, channelName: "rabbit-orders");

// Publish a pre-built CloudEvent to a specific named channel
await publisher.PublishEventAsync(@event, channelName: "rabbit-notifications");
```

These are convenience overloads on `EventPublisher` and wrap the name in a `NamedChannelPublishOptions` internally, so no channel-specific knowledge is needed at the call site.

### Per-call options with `ChannelName`

To target a named channel **and** supply per-call overrides at the same time, set `ChannelName` directly on the concrete options instance:

```csharp
await publisher.PublishEventAsync(@event, new RabbitMqPublishOptions
{
    ChannelName  = "rabbit-orders",
    RoutingKey   = "order.priority",
});
```

The publisher first filters the channel list to those matching `"rabbit-orders"`, then forwards the full options object to the matching channel.

### `NamedChannelPublishOptions`

When you only need to target a channel by name without providing any transport-specific overrides, use `NamedChannelPublishOptions` directly:

```csharp
await publisher.PublishEventAsync(@event, new NamedChannelPublishOptions("rabbit-orders"));

// Equivalent to the shorthand extension:
await publisher.PublishEventAsync(@event, "rabbit-orders");
```

---

## Filtering rules

| Channel implements `INamedEventPublishChannel`? | Channel `Name` | Filter `ChannelName` in options | Result |
|---|---|---|---|
| No (anonymous) | — | any | **Receives the event** |
| Yes | `null` or empty | any | **Receives the event** (treated as anonymous) |
| Yes | `"rabbit-orders"` | `null` or absent | **Receives the event** (no filter) |
| Yes | `"rabbit-orders"` | `"rabbit-orders"` | **Receives the event** ✓ |
| Yes | `"rabbit-orders"` | `"rabbit-notifications"` | **Skipped** |

> **No-match is a silent no-op.**  If the name filter matches no registered channel the event is simply not delivered (no exception is thrown).  Enable structured logging and set the log level to `Debug` or `Trace` to diagnose routing issues.

---

## Named channels with `CombinedPublishOptions`

`CombinedPublishOptions` does **not** implement `INamedChannelFilter` — setting `ChannelName` on the combined wrapper itself would create ambiguity about which bundled entries it applies to.

Instead, set `ChannelName` on **each individual bundled entry**.  The publisher uses it during per-entry resolution to match the entry to the correct named channel:

```csharp
var overrides = EventPublishOptions.Combine(
    // → targets the "rabbit-orders" channel only
    new RabbitMqPublishOptions
    {
        ChannelName  = "rabbit-orders",
        RoutingKey   = "order.placed",
    },
    // → targets the "rabbit-notifications" channel only
    new RabbitMqPublishOptions
    {
        ChannelName  = "rabbit-notifications",
        RoutingKey   = "notification.sent",
    });

await publisher.PublishEventAsync(@event, overrides);
```

Each bundled entry is matched independently: type-assignability **and** name must both agree for an entry to be forwarded to a channel.

---

## Named test channels

When testing code that uses named channels, pass the same name to `AddTestChannel`:

```csharp
var ordersEvents       = new List<CloudEvent>();
var notificationEvents = new List<CloudEvent>();

services.AddEventPublisher()
        .AddTestChannel(@ev => ordersEvents.Add(@ev),       channelName: "rabbit-orders")
        .AddTestChannel(@ev => notificationEvents.Add(@ev), channelName: "rabbit-notifications");
```

Each test channel will only fire for events whose per-call options carry the matching `ChannelName`.

---

## Implementing a custom named channel

Any channel can participate in name-based routing by implementing `INamedEventPublishChannel`:

```csharp
public class KafkaEventPublishChannel : IEventPublishChannel, INamedEventPublishChannel
{
    private readonly KafkaChannelOptions _options;

    public KafkaEventPublishChannel(IOptions<KafkaChannelOptions> options)
        => _options = options.Value;

    // Expose the name from channel-level options
    public string? Name => _options.ChannelName;

    public async Task PublishAsync(
        CloudEvent @event,
        EventPublishOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        // ... deliver via Confluent.Kafka
    }
}
```

Channels that extend `EventPublishChannel<TOptions>` get `INamedEventPublishChannel` for free as long as the options class implements `INamedChannelFilter`.

---

## Related pages

- [Publish Channels](../concepts/publish-channels.md)
- [Event Publisher](../concepts/event-publisher.md)
- [Typed Channels](typed-channels.md)
- [Test Publisher](../testing/README.md)

