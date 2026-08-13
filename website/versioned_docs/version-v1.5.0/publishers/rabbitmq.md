# RabbitMQ Channel

The `Hermodr.Publisher.RabbitMq` package adds a publish channel that delivers `CloudEvent` instances to a RabbitMQ exchange using the official `RabbitMQ.Client` library.

## Installation

```bash
dotnet add package Hermodr.Publisher.RabbitMq
```

## Registration

### Inline configuration

```csharp
using Hermodr;

builder.Services
    .AddEventPublisher()
    .AddRabbitMq(options =>
    {
        options.ConnectionString = "amqp://guest:guest@localhost:5672";
        options.ExchangeName     = "events";
        options.RoutingKey       = "my.service";
    });
```

### From `appsettings.json`

```csharp
builder.Services
    .AddEventPublisher()
    .AddRabbitMq("Events:RabbitMq");
```

```json
// appsettings.json
{
  "Events": {
    "RabbitMq": {
      "ConnectionString": "amqp://guest:guest@localhost:5672",
      "ExchangeName": "events",
      "RoutingKey": "my.service",
      "QueueName": "my-service-events"
    }
  }
}
```

## Options reference

`RabbitMqPublishOptions`

| Property | Type | Effective default | Description |
|----------|------|-------------------|-------------|
| `ConnectionString` | `string?` | `null` | AMQP connection URI |
| `ExchangeName` | `string?` | `null` | Exchange to publish to (falls back to per-event `[AmqpExchange]` annotation) |
| `RoutingKey` | `string?` | `null` | Default routing key (falls back to per-event `[AmqpRoutingKey]` annotation) |
| `QueueName` | `string?` | `null` | Optional queue to bind the exchange to |
| `ClientName` | `string?` | `"Hermodr"` | Client name shown in RabbitMQ management UI |
| `MessageFormat` | `RabbitMqMessageFormat?` | `Json` | Serialisation format: `Json` or `Binary`. `null` → use channel default |
| `MessageContent` | `RabbitMqMessageContent?` | `CloudEvent` | Whether to send the full `CloudEvent` envelope or only the `data` payload. `null` → use channel default |
| `PersistentMessages` | `bool?` | `true` | Delivery mode 2 (survives broker restarts). `null` → use channel default |
| `PublisherConfirms` | `bool?` | `true` | Wait for broker acknowledgement before returning. `null` → use channel default |
| `ConfirmTimeout` | `TimeSpan?` | 5 s | Timeout for publisher confirms. `null` → use channel default |
| `Mandatory` | `bool?` | `false` | Return unroutable messages instead of silently dropping them. `null` → use channel default |
| `JsonSerializerOptions` | `JsonSerializerOptions?` | default | Serialisation options when `MessageFormat = Json` |

> **Nullable value-type properties** (`MessageFormat`, `MessageContent`, `PersistentMessages`, `PublisherConfirms`, `ConfirmTimeout`, `Mandatory`) use `null` as the sentinel meaning *"inherit from the channel-level default"*.  Set them in the channel registration to establish the baseline; set them in a per-call override only when you need to deviate from that baseline for a specific delivery.

The channel sets AMQP `BasicProperties.Timestamp` from `CloudEvent.time` when available.
If `CloudEvent.time` is missing, it falls back to `IEventSystemTime.UtcNow`, so tests can freeze the publish clock via `UseSystemTime<TClock>()`.

## Per-delivery options

Pass a `RabbitMqPublishOptions` instance as the second argument to `PublishAsync` to override individual properties for a single publish call.  Only the properties you set (non-`null`) replace the channel default — all others fall back to the values configured at registration time.

```csharp
// Resolve the concrete channel directly from DI.
var channel = serviceProvider.GetRequiredService<RabbitMqPublishChannel>();

// Override only the routing key and make this one message non-persistent.
// Everything else (ConnectionString, ExchangeName, PublisherConfirms, …)
// is inherited from the channel-level defaults.
await channel.PublishAsync(@event, new RabbitMqPublishOptions
{
    RoutingKey         = "priority.orders",
    PersistentMessages = false,
});
```

## Typed channel

Use `AddRabbitMq<TEvent>()` to register a channel that receives **only** events whose data class is `TEvent`.  The typed channel subclass (`RabbitMqPublishChannel<TEvent>`) merges the general `RabbitMqPublishOptions` with the type-specific `RabbitMqPublishOptions<TEvent>` at construction time: non-`null` typed values win; `null` values fall back to the base defaults.

```csharp
builder.Services
    .AddEventPublisher()
    // Shared defaults
    .AddRabbitMq(opts =>
    {
        opts.ConnectionString    = "amqp://guest:guest@localhost:5672";
        opts.ExchangeName        = "events";
        opts.PersistentMessages  = true;
        opts.PublisherConfirms   = true;
    })
    // OrderPlaced events route to a dedicated exchange/queue
    .AddRabbitMq<OrderPlaced>(opts =>
    {
        opts.ExchangeName = "orders";
        opts.QueueName    = "order-placed";
        opts.RoutingKey   = "order.placed";
        // ConnectionString, PersistentMessages, PublisherConfirms inherited from base
    });
```

From configuration, bind the typed options from a nested section:

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

See [Typed Channels](typed-channels.md) for a full explanation of the two-level options hierarchy and the merge rules.

## AMQP Annotations

Install the `Hermodr.Amqp.Annotations` package to declare routing metadata on individual event data classes, overriding the global channel defaults on a per-event-type basis.

```bash
dotnet add package Hermodr.Amqp.Annotations
```

### `[AmqpExchange]`

Declares the AMQP exchange that events of this type should be published to.

```csharp
using Hermodr;

[Event("order.placed", "1.0")]
[AmqpExchange("orders")]
public class OrderPlaced
{
    public Guid OrderId { get; set; }
    public decimal Amount { get; set; }
}
```

When the RabbitMQ channel publishes an `OrderPlaced` event, it targets the `"orders"` exchange, overriding any global `ExchangeName` set in `RabbitMqPublishOptions`.

### `[AmqpRoutingKey]`

Declares the routing key to use when publishing an event to the exchange.

```csharp
[Event("order.placed", "1.0")]
[AmqpExchange("orders")]
[AmqpRoutingKey("order.placed")]
public class OrderPlaced
{
    public Guid OrderId { get; set; }
    public decimal Amount { get; set; }
}
```

### Priority rules

When multiple sources of AMQP routing metadata exist, the following priority applies (highest to lowest):

1. Per-event attributes (`[AmqpExchange]`, `[AmqpRoutingKey]`)
2. `RabbitMqPublishOptions.ExchangeName` / `.RoutingKey`

### Complete example

```csharp
using System.ComponentModel.DataAnnotations;
using Hermodr;

[Event("inventory.low-stock", "1.0", Description = "Raised when a product is running low on stock")]
[AmqpExchange("inventory")]
[AmqpRoutingKey("inventory.low-stock")]
public class LowStock
{
    [Required]
    public string ProductId { get; set; } = default!;

    [Required]
    [Range(0, int.MaxValue)]
    public int RemainingQuantity { get; set; }
}
```

```csharp
// Program.cs
builder.Services
    .AddEventPublisher()
    .AddRabbitMq(options =>
    {
        options.ConnectionString = "amqp://guest:guest@localhost:5672";
        // ExchangeName and RoutingKey are overridden per event by annotations
    });
```

```csharp
// Publishing
await publisher.PublishAsync(new LowStock
{
    ProductId         = "PROD-42",
    RemainingQuantity = 3
});
// → Published to exchange "inventory" with routing key "inventory.low-stock"
```

### Internal implementation

Both `[AmqpExchange]` and `[AmqpRoutingKey]` extend `EventAttributesAttribute`, which injects named CloudEvent extension attributes into the envelope:

| Attribute | CloudEvent extension attribute |
|-----------|-------------------------------|
| `[AmqpExchange("orders")]` | `amqp-exchange-name = "orders"` |
| `[AmqpRoutingKey("order.placed")]` | `amqp-routing-key = "order.placed"` |

The RabbitMQ channel reads these extension attributes from the `CloudEvent` envelope at delivery time.

## Message formats

### `RabbitMqMessageFormat`

| Value | Description |
|-------|-------------|
| `Json` | Serialise as JSON (default) |
| `Xml` | Serialise as XML |

### `RabbitMqMessageContent`

| Value | Description |
|-------|-------------|
| `CloudEvent` | Send the full CloudEvent envelope (default) |
| `DataOnly` | Send only the `data` portion of the CloudEvent |

## Custom connection factory

Implement `IRabbitMqConnectionFactory` to supply a custom `IConnection`:

```csharp
public class MyConnectionFactory : IRabbitMqConnectionFactory
{
    public async Task<IConnection> CreateConnectionAsync()
    {
        var factory = new ConnectionFactory { Uri = new Uri("amqp://guest:guest@localhost:5672") };
        return await factory.CreateConnectionAsync();
    }
}
```

Register it after the channel:

```csharp
builder.Services
    .AddEventPublisher()
    .AddRabbitMq(options => { /* ... */ });
builder.Services
    .AddSingleton<IRabbitMqConnectionFactory, MyConnectionFactory>();
```

## Related pages

- [Publisher Channels Overview](README.md)
- [Typed Channels](typed-channels.md)
- [Event Annotations](../concepts/event-annotations.md)



