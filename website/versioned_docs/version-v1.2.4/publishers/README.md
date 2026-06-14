# Publisher Channels Overview

Hermodr ships ready-made channel implementations for the most common messaging transports, plus reliability-oriented publisher extensions such as the transactional outbox and dead-letter replay. You install only what you need and register the pieces in the `EventPublisherBuilder` chain.

## Available channels and reliability extensions

| Feature | Package | Best for |
|---------|---------|----------|
| [Azure Service Bus](azure-service-bus.md) | `Hermodr.Publisher.AzureServiceBus` | Cloud-native apps on Azure, reliable queue- or topic-based delivery |
| [RabbitMQ](rabbitmq.md) | `Hermodr.Publisher.RabbitMq` | On-premise or self-hosted AMQP broker, fine-grained routing |
| [MassTransit](masstransit.md) | `Hermodr.Publisher.MassTransit` | Projects already using MassTransit; broker-agnostic |
| [Webhook](webhook.md) | `Hermodr.Publisher.Webhook` | Delivering events to external HTTP endpoints with HMAC signing |
| [Publish Error Handling](error-handling.md) | `Hermodr.Publisher` | Cross-cutting interception of publish failures for logging, policy, auditing, and custom recovery |
| [OpenTelemetry Instrumentation](opentelemetry.md) | `Hermodr.Publisher.OpenTelemetry` | Distributed tracing with W3C trace context propagation across service boundaries |
| [Transactional Outbox](outbox.md) | `Hermodr.Publisher.Outbox` | Guaranteed at-least-once delivery via a transactional outbox table |
| [Dead-Letter Handling and Replay](dead-letter.md) | `Hermodr.Publisher.DeadLetter` | Capturing failed channel deliveries, persisting them, and replaying them later |
| [Test (in-memory)](../testing/README.md) | `Hermodr.TestPublisher` | Unit and integration tests |

## Multiple channels

You can register more than one channel at a time.  The publisher will fan every event out to **all** registered channels:

```csharp
builder.Services
    .AddEventPublisher(options => options.Source = new Uri("https://myapp.example.com"))
    .AddServiceBus(options =>
    {
        options.ConnectionString = "...";
        options.QueueName = "events";
    })
    .AddWebhooks(options =>
    {
        options.EndpointUrl  = "https://partner.example.com/events";
        options.SigningSecret = "s3cr3t";
    });
```

## Named channels

When two or more channels of the same transport type are registered simultaneously, assign each a **logical name** via the `ChannelName` property on its options.  At publish time, target the right channel by name using the convenience extension methods or by setting `ChannelName` on the per-call options:

```csharp
builder.Services
    .AddEventPublisher()
    .AddRabbitMq(opts => { opts.ChannelName = "rabbit-orders"; opts.ExchangeName = "orders"; })
    .AddRabbitMq(opts => { opts.ChannelName = "rabbit-notifications"; opts.ExchangeName = "notifications"; });

// Publish only to the "rabbit-orders" channel
await publisher.PublishAsync(orderPlaced, channelName: "rabbit-orders");
```

See [Named Channels](named-channels.md) for the full guide.

## Typed channels

Every built-in channel supports a **typed** registration variant (`AddRabbitMq<TEvent>()`, `AddServiceBus<TEvent>()`, `AddMassTransit<TEvent>()`, `AddWebhooks<TEvent>()`) that routes **only** events of the specified data class to that channel.  Typed channels also support a two-level options hierarchy — a base set of defaults merged with per-event-type overrides — so you can share common settings and specialise only what differs.

```csharp
builder.Services
    .AddEventPublisher()
    .AddRabbitMq(opts => { opts.ConnectionString = "amqp://..."; opts.ExchangeName = "events"; })
    .AddRabbitMq<OrderPlacedData>(opts => { opts.ExchangeName = "orders"; opts.QueueName = "order-placed"; });
```

See [Typed Channels](typed-channels.md) for the full guide.

## Implementing a custom channel

See [Publish Channels](../concepts/publish-channels.md#implementing-a-custom-channel) for instructions on creating and registering your own `IEventPublishChannel` or `IEventPublishChannel<TEvent>`.
