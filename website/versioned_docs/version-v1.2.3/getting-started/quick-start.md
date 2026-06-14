# Quick Start

This guide walks you through publishing your first event in under five minutes.

## 1. Install the packages

```bash
dotnet add package Deveel.Events.Publisher
dotnet add package Deveel.Events.Annotations
```

Add a channel package for the transport you want to use, for example Azure Service Bus:

```bash
dotnet add package Deveel.Events.Publisher.AzureServiceBus
```

## 2. Annotate your event data class

Create a class that represents the data payload of your event and decorate it with `[Event]`:

```csharp
using System.ComponentModel.DataAnnotations;
using Deveel.Events;

[Event("order.placed", "1.0")]
public class OrderPlacedData
{
    [Required]
    public Guid OrderId { get; set; }

    [Required]
    [Range(0.01, 1_000_000.0)]
    public decimal Amount { get; set; }

    [Required]
    public string Currency { get; set; } = default!;

    public string? Notes { get; set; }
}
```

The `[Event]` attribute records the **event type** string and its **version**.  The framework uses these values when constructing the `CloudEvent` envelope.

## 3. Register the publisher

In your `Program.cs` (or wherever you configure DI), register the event publisher and the Azure Service Bus channel:

```csharp
using Deveel.Events;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddEventPublisher(options =>
    {
        options.Source = new Uri("https://myapp.example.com");
    })
    .AddServiceBus(options =>
    {
        options.ConnectionString = builder.Configuration["ServiceBus:ConnectionString"]!;
        options.QueueName        = builder.Configuration["ServiceBus:QueueName"]!;
    });
```

> **Tip:** You can also bind options from `appsettings.json` by passing a configuration-section path instead of a delegate — see [Azure Service Bus](../publishers/azure-service-bus.md).

## 4. Inject and publish

Inject `EventPublisher` wherever you need it and call `PublishAsync`:

```csharp
using Deveel.Events;

public class OrderService
{
    private readonly EventPublisher _publisher;

    public OrderService(EventPublisher publisher)
    {
        _publisher = publisher;
    }

    public async Task PlaceOrderAsync(Guid orderId, decimal amount, string currency)
    {
        var data = new OrderPlacedData
        {
            OrderId  = orderId,
            Amount   = amount,
            Currency = currency
        };

        // The framework builds the CloudEvent envelope from the [Event] attribute
        // and publishes it to all registered channels.
        await _publisher.PublishAsync(data);
    }
}
```

## 5. Publishing a raw CloudEvent

If you already have a `CloudEvent` instance you can publish it directly:

```csharp
using CloudNative.CloudEvents;
using Deveel.Events;

var @event = new CloudEvent
{
    Type    = "com.example.myevent",
    Source  = new Uri("https://myapp.example.com"),
    DataContentType = "application/json",
    Data    = new { Message = "Hello, World!" }
};

await publisher.PublishEventAsync(@event);
```

## What's next?

| Topic | Description |
|-------|-------------|
| [Core Concepts](../concepts/README.md) | Understand how the pieces fit together |
| [Publisher Channels](../publishers/README.md) | Configure specific transports |
| [Event Schema](../schema/README.md) | Validate events before publishing |
| [Testing](../testing/README.md) | Unit-test your event publishing |

