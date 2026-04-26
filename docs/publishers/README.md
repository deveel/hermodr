# Publisher Channels Overview

Deveel Events ships ready-made channel implementations for the most common messaging transports.  You install only what you need and register the channel(s) in the `EventPublisherBuilder` chain.

## Available channels

| Channel | Package | Best for |
|---------|---------|----------|
| [Azure Service Bus](azure-service-bus.md) | `Deveel.Events.Publisher.AzureServiceBus` | Cloud-native apps on Azure, reliable queue- or topic-based delivery |
| [RabbitMQ](rabbitmq.md) | `Deveel.Events.Publisher.RabbitMq` | On-premise or self-hosted AMQP broker, fine-grained routing |
| [MassTransit](masstransit.md) | `Deveel.Events.Publisher.MassTransit` | Projects already using MassTransit; broker-agnostic |
| [Webhook](webhook.md) | `Deveel.Events.Publisher.Webhook` | Delivering events to external HTTP endpoints with HMAC signing |
| [Test (in-memory)](../testing/README.md) | `Deveel.Events.TestPublisher` | Unit and integration tests |

## Multiple channels

You can register more than one channel at a time.  The publisher will fan every event out to **all** registered channels:

```csharp
builder.Services
    .AddEventPublisher(options => options.Source = new Uri("https://myapp.example.com"))
    .AddServiceBusChannel(options =>
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

## Typed channels

Every built-in channel supports a **typed** registration variant (`AddRabbitMq<TEvent>()`, `AddServiceBusChannel<TEvent>()`, `AddMassTransit<TEvent>()`, `AddWebhooks<TEvent>()`) that routes **only** events of the specified data class to that channel.  Typed channels also support a two-level options hierarchy — a base set of defaults merged with per-event-type overrides — so you can share common settings and specialise only what differs.

```csharp
builder.Services
    .AddEventPublisher()
    .AddRabbitMq(opts => { opts.ConnectionString = "amqp://..."; opts.ExchangeName = "events"; })
    .AddRabbitMq<OrderPlacedData>(opts => { opts.ExchangeName = "orders"; opts.QueueName = "order-placed"; });
```

See [Typed Channels](typed-channels.md) for the full guide.

## Implementing a custom channel

See [Publish Channels](../concepts/publish-channels.md#implementing-a-custom-channel) for instructions on creating and registering your own `IEventPublishChannel` or `IEventPublishChannel<TEvent>`.

