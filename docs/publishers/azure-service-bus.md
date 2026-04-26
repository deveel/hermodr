# Azure Service Bus Channel

The `Deveel.Events.Publisher.AzureServiceBus` package adds a publish channel that serialises `CloudEvent` instances as `ServiceBusMessage` objects and delivers them to an Azure Service Bus queue or topic.

## Installation

```bash
dotnet add package Deveel.Events.Publisher.AzureServiceBus
```

## Registration

### Inline configuration

```csharp
using Deveel.Events;

builder.Services
    .AddEventPublisher()
    .AddServiceBusChannel(options =>
    {
        options.ConnectionString = "<your-connection-string>";
        options.QueueName        = "events";
    });
```

### From `appsettings.json`

```csharp
builder.Services
    .AddEventPublisher()
    .AddServiceBusChannel("Events:ServiceBus");
```

```json
// appsettings.json
{
  "Events": {
    "ServiceBus": {
      "ConnectionString": "<your-connection-string>",
      "QueueName": "events"
    }
  }
}
```

## Options reference

`ServiceBusEventPublishChannelOptions`

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `ConnectionString` | `string` | ✅ | Azure Service Bus connection string. In a per-call override an empty/whitespace value falls back to the channel default. |
| `QueueName` | `string` | ✅ | Name of the queue or topic to publish to. In a per-call override an empty/whitespace value falls back to the channel default. |
| `ClientOptions` | `ServiceBusClientOptions` | | Advanced client settings (see Azure SDK docs). `null` in a per-call override falls back to the channel default. |

## Typed channel

Use `AddServiceBusChannel<TEvent>()` to register a channel that receives **only** events whose data class is `TEvent`.  At construction time the typed channel (`ServiceBusEventPublishChannel<TEvent>`) merges the general `ServiceBusEventPublishChannelOptions` with the type-specific `ServiceBusEventPublishChannelOptions<TEvent>`: non-empty typed values win; empty or `null` values fall back to the base defaults.

> **Note:** `ServiceBusEventPublishChannelOptions<TEvent>` re-declares `ConnectionString` and `QueueName` as nullable (`string?`) so that leaving them `null` is the unambiguous signal to inherit from the base options.  The required, non-nullable constraint from the base class is enforced only after merging.

```csharp
builder.Services
    .AddEventPublisher()
    // General channel — shared connection & default queue
    .AddServiceBusChannel(opts =>
    {
        opts.ConnectionString = "<connection-string>";
        opts.QueueName        = "events";
    })
    // OrderPlaced events go to a dedicated queue
    .AddServiceBusChannel<OrderPlacedData>(opts =>
    {
        opts.QueueName = "order-placed";
        // ConnectionString inherited from base options
    });
```

From configuration:

```csharp
builder.Services
    .AddEventPublisher()
    .AddServiceBusChannel("Events:ServiceBus")
    .AddServiceBusChannel<OrderPlacedData>("Events:ServiceBus:Orders");
```

```json
{
  "Events": {
    "ServiceBus": {
      "ConnectionString": "<connection-string>",
      "QueueName": "events",
      "Orders": {
        "QueueName": "order-placed"
      }
    }
  }
}
```

See [Typed Channels](typed-channels.md) for the full merge semantics and further examples.

## How it works

1. The channel resolves a `ServiceBusClient` via `IServiceBusClientFactory`.
2. Each `CloudEvent` is serialised to JSON and wrapped in a `ServiceBusMessage`.
3. CloudEvent attributes (`id`, `type`, `source`, `time`, `datacontenttype`) are mapped to message application properties so consumers can filter without parsing the body.
4. The message is sent using a `ServiceBusSender` for the configured queue name.

## Per-delivery options

Every channel registered with `AddServiceBusChannel` also implements `IEventPublishChannel<ServiceBusEventPublishChannelOptions>`, letting you override the destination queue (and, optionally, `ClientOptions`) for a single publish call.  Any property you leave empty or `null` in the per-call override falls back to the channel default.

```csharp
// Resolve the typed channel from DI
var channel = serviceProvider
    .GetRequiredService<IEventPublishChannel<ServiceBusEventPublishChannelOptions>>();

// Send this particular event to a different queue,
// while inheriting ConnectionString and ClientOptions from the channel defaults.
await channel.PublishAsync(@event, new ServiceBusEventPublishChannelOptions
{
    QueueName = "priority-events",
});
```

## Custom client factory

If you need to control how the `ServiceBusClient` is created (e.g. for managed identity), implement `IServiceBusClientFactory`:

```csharp
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Deveel.Events;

public class ManagedIdentityServiceBusClientFactory : IServiceBusClientFactory
{
    private readonly ServiceBusEventPublishChannelOptions _options;

    public ManagedIdentityServiceBusClientFactory(IOptions<ServiceBusEventPublishChannelOptions> options)
    {
        _options = options.Value;
    }

    public ServiceBusClient CreateClient()
        => new ServiceBusClient(_options.ConnectionString, new DefaultAzureCredential());
}
```

Then register it (it will replace the built-in factory):

```csharp
builder.Services
    .AddEventPublisher()
    .AddServiceBusChannel(options => options.QueueName = "events")
    .Services
        .AddSingleton<IServiceBusClientFactory, ManagedIdentityServiceBusClientFactory>();
```

## Related pages

- [Publisher Channels Overview](README.md)
- [Typed Channels](typed-channels.md)
- [Event Publisher](../concepts/event-publisher.md)

