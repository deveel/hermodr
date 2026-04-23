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
| `ConnectionString` | `string` | ✅ | Azure Service Bus connection string |
| `QueueName` | `string` | ✅ | Name of the queue or topic to publish to |
| `ClientOptions` | `ServiceBusClientOptions` | | Advanced client settings (see Azure SDK docs) |

## How it works

1. The channel resolves a `ServiceBusClient` via `IServiceBusClientFactory`.
2. Each `CloudEvent` is serialised to JSON and wrapped in a `ServiceBusMessage`.
3. CloudEvent attributes (`id`, `type`, `source`, `time`, `datacontenttype`) are mapped to message application properties so consumers can filter without parsing the body.
4. The message is sent using a `ServiceBusSender` for the configured queue name.

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

