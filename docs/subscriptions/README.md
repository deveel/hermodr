# Event Subscriptions

The `Deveel.Events.Subscriptions` package extends the publisher pipeline with an **event dispatcher** that routes published `CloudEvent`s to registered subscriber handlers.

## Overview

Subscriptions are evaluated by one or more `IEventSubscriptionResolver` implementations. The built-in `EventSubscriptionRegistry` keeps subscriptions in memory, but resolvers can read from any source — a local store, a relational database, a remote subscription service, or any combination. In future releases, subscription evaluation may even be delegated entirely to a remote system.

## Why Use Subscriptions?

- **Fan-out within an application** — notify multiple services without a broker.
- **Integration testing** — assert that specific events are handled without external infrastructure.
- **Side-effects on publish** — trigger audit logging, cache invalidation, or read-model projection.
- **Event routing** — conditionally re-publish events to a different channel based on payload content.
- **Dynamic, database-backed rules** — load subscription filters from a store, update them at runtime, and let resolvers pick up the changes without restarting the application.

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                      Your application                       │
│                                                             │
│   ┌──────────────┐      ┌────────────────────────────┐      │
│   │  Event data  │─────▶│      IEventPublisher       │      │
│   └──────────────┘      └────────────┬───────────────┘      │
│                                      │ fan-out              │
│                       ┌──────────────┴──────────────┐       │
│                       │                             │       │
│              ┌────────▼────────┐         ┌──────────▼───┐   │
│              │ EventDispatcher │         │  Channel B   │   │
│              │  (IEventPublish │         │ (Azure SB …) │   │
│              │    Channel)     │         └──────────────┘   │
│              └────────┬────────┘                            │
│                       │ resolves matching subscriptions     │
│              ┌────────┴──────────────────────────────────┐  │
│              │  IEventSubscriptionResolver (× N)         │  │
│              │  ┌──────────────────────────────────────┐ │  │
│              │  │  EventSubscriptionRegistry (default) │ │  │
│              │  │  DatabaseResolver / RemoteResolver … │ │  │
│              │  └──────────────────────────────────────┘ │  │
│              └───────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
```

1. You register the dispatcher with `AddDispatcher()`.
2. The dispatcher is registered as an `IEventPublishChannel` — it receives every event published through `IEventPublisher`.
3. For each event, the dispatcher queries **all** registered `IEventSubscriptionResolver` instances and collects matching subscriptions.
4. Every matched subscription's `HandleAsync` is invoked sequentially.

## Quick Start

### 1. Install the package

```bash
dotnet add package Deveel.Events.Subscriptions
```

### 2. Register the dispatcher

```csharp
services.AddEventPublisher(pub => pub
    .AddDispatcher()
    .Subscribe("com.example.order.*", async (cloudEvent, ct) =>
    {
        Console.WriteLine($"Order event received: {cloudEvent.Type}");
        await Task.CompletedTask;
    }, name: "log-orders"));
```

### 3. Publish — subscriptions fire automatically

```csharp
var publisher = serviceProvider.GetRequiredService<IEventPublisher>();
await publisher.PublishAsync(new OrderPlaced { OrderId = "42" });
// ↑ "log-orders" subscription fires because "com.example.order.placed" matches "com.example.order.*"
```

## Pages in This Section

| Page | Description |
|------|-------------|
| [Subscription Filters](filtering.md) | `CloudEventFilter` factory methods, envelope attribute filters, data-field filters, and combining expressions |
| [Filter Expressions](filter-expressions.md) | The `FilterExpression` model, `FilterExpressionType`, serialization, and direct evaluation |
| [Event Dispatcher](dispatcher.md) | How the dispatcher works, DI setup, and error handling |
| [Routing Subscriptions](routing.md) | Re-publishing matched events to a different channel |
| [Custom Resolvers](custom-resolver.md) | Reading subscriptions from a database or remote service |
