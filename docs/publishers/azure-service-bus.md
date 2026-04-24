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
- [Event Publisher](../concepts/event-publisher.md)

