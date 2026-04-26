# RabbitMQ Channel

The `Deveel.Events.Publisher.RabbitMq` package adds a publish channel that delivers `CloudEvent` instances to a RabbitMQ exchange using the official `RabbitMQ.Client` library.

## Installation

```bash
dotnet add package Deveel.Events.Publisher.RabbitMq
```

## Registration

### Inline configuration

```csharp
using Deveel.Events;

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

`RabbitMqEventPublishOptions`

| Property | Type | Effective default | Description |
|----------|------|-------------------|-------------|
| `ConnectionString` | `string?` | `null` | AMQP connection URI |
| `ExchangeName` | `string?` | `null` | Exchange to publish to (falls back to per-event `[AmqpExchange]` annotation) |
| `RoutingKey` | `string?` | `null` | Default routing key (falls back to per-event `[AmqpRoutingKey]` annotation) |
| `QueueName` | `string?` | `null` | Optional queue to bind the exchange to |
| `ClientName` | `string?` | `"Deveel.Events"` | Client name shown in RabbitMQ management UI |
| `MessageFormat` | `RabbitMqMessageFormat?` | `Json` | Serialisation format: `Json` or `Binary`. `null` → use channel default |
| `MessageContent` | `RabbitMqMessageContent?` | `CloudEvent` | Whether to send the full `CloudEvent` envelope or only the `data` payload. `null` → use channel default |
| `PersistentMessages` | `bool?` | `true` | Delivery mode 2 (survives broker restarts). `null` → use channel default |
| `PublisherConfirms` | `bool?` | `true` | Wait for broker acknowledgement before returning. `null` → use channel default |
| `ConfirmTimeout` | `TimeSpan?` | 5 s | Timeout for publisher confirms. `null` → use channel default |
| `Mandatory` | `bool?` | `false` | Return unroutable messages instead of silently dropping them. `null` → use channel default |
| `JsonSerializerOptions` | `JsonSerializerOptions?` | default | Serialisation options when `MessageFormat = Json` |

> **Nullable value-type properties** (`MessageFormat`, `MessageContent`, `PersistentMessages`, `PublisherConfirms`, `ConfirmTimeout`, `Mandatory`) use `null` as the sentinel meaning *"inherit from the channel-level default"*.  Set them in the channel registration to establish the baseline; set them in a per-call override only when you need to deviate from that baseline for a specific delivery.

## Per-delivery options

Pass a `RabbitMqEventPublishOptions` instance as the second argument to `PublishAsync` to override individual properties for a single publish call.  Only the properties you set (non-`null`) replace the channel default — all others fall back to the values configured at registration time.

```csharp
// Resolve the concrete channel directly from DI.
var channel = serviceProvider.GetRequiredService<RabbitMqEventPublishChannel>();

// Override only the routing key and make this one message non-persistent.
// Everything else (ConnectionString, ExchangeName, PublisherConfirms, …)
// is inherited from the channel-level defaults.
await channel.PublishAsync(@event, new RabbitMqEventPublishOptions
{
    RoutingKey         = "priority.orders",
    PersistentMessages = false,
});
```

## AMQP Annotations

Install the `Deveel.Events.Amqp.Annotations` package to declare routing metadata on individual event data classes, overriding the global channel defaults on a per-event-type basis.

```bash
dotnet add package Deveel.Events.Amqp.Annotations
```

### `[AmqpExchange]`

Declares the AMQP exchange that events of this type should be published to.

```csharp
using Deveel.Events;

[Event("order.placed", "1.0")]
[AmqpExchange("orders")]
public class OrderPlacedData
{
    public Guid OrderId { get; set; }
    public decimal Amount { get; set; }
}
```

When the RabbitMQ channel publishes an `OrderPlacedData` event, it targets the `"orders"` exchange, overriding any global `ExchangeName` set in `RabbitMqEventPublishChannelOptions`.

### `[AmqpRoutingKey]`

Declares the routing key to use when publishing an event to the exchange.

```csharp
[Event("order.placed", "1.0")]
[AmqpExchange("orders")]
[AmqpRoutingKey("order.placed")]
public class OrderPlacedData
{
    public Guid OrderId { get; set; }
    public decimal Amount { get; set; }
}
```

### Priority rules

When multiple sources of AMQP routing metadata exist, the following priority applies (highest to lowest):

1. Per-event attributes (`[AmqpExchange]`, `[AmqpRoutingKey]`)
2. `RabbitMqEventPublishChannelOptions.ExchangeName` / `.RoutingKey`

### Complete example

```csharp
using System.ComponentModel.DataAnnotations;
using Deveel.Events;

[Event("inventory.low-stock", "1.0", Description = "Raised when a product is running low on stock")]
[AmqpExchange("inventory")]
[AmqpRoutingKey("inventory.low-stock")]
public class LowStockData
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
await publisher.PublishAsync(new LowStockData
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
    .AddRabbitMq(options => { /* ... */ })
    .Services
        .AddSingleton<IRabbitMqConnectionFactory, MyConnectionFactory>();
```

## Related pages

- [Publisher Channels Overview](README.md)
- [Event Annotations](../concepts/event-annotations.md)



