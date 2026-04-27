# Routing Subscriptions

A **routing subscription** intercepts a matched `CloudEvent` and **re-publishes** it through the `IEventPublisher` pipeline, optionally targeting a specific channel. This is useful for conditional fan-out, format conversion, or splitting a single publish call into multiple targeted deliveries.

## How It Works

`RoutingEventSubscription` implements `IEventSubscription`. When an event matches its filter, it resolves `IEventPublisher` from the DI container (lazily, to avoid circular dependencies) and re-publishes the event using optional `EventPublishOptions` to select the target channel.

```
IEventPublisher.PublishAsync(event)
  └── EventDispatcher receives event (as IEventPublishChannel)
        └── Filter matches RoutingEventSubscription
              └── RoutingEventSubscription.HandleAsync(event)
                    └── IEventPublisher.PublishEventAsync(event, routingOptions)
                          └── Routed to the target channel (e.g. Azure Service Bus queue)
```

> **Circular dependency:** The publisher depends on the dispatcher (as an `IEventPublishChannel`), which in turn invokes the routing subscription, which calls back into the publisher. This is safe because `IEventPublisher` is resolved **lazily** (on the first `HandleAsync` call), after the DI container is fully built.

---

## Registration

Use the `RouteToChannel` extension methods on `EventPublisherBuilder`:

### By Type Pattern

```csharp
pub.AddDispatcher()
   .RouteToChannel(
       typePattern: "com.example.order.*",
       routingOptions: new EventPublishOptions { ChannelName = "orders-bus" },
       name: "route-orders-to-bus");
```

### By Pre-Built Filter

```csharp
var filter = EventSubscriptionFilter.Builder
    .WithTypePattern("com.example.order.*")
    .WithExtension("priority", "high")
    .Build();

pub.AddDispatcher()
   .RouteToChannel(
       filter: filter,
       routingOptions: new EventPublishOptions { ChannelName = "high-priority-bus" },
       name: "route-high-priority");
```

### With Fluent Filter Builder

```csharp
pub.AddDispatcher()
   .RouteToChannel(
       configureFilter: fb => fb
           .WithTypePattern("com.example.payment.*")
           .WithJsonPath("currency", "USD"),
       routingOptions: new EventPublishOptions { ChannelName = "usd-payments" },
       name: "route-usd-payments");
```

---

## Routing Options

`EventPublishOptions` selects the target channel:

| Property | Description |
|----------|-------------|
| `ChannelName` | Route to the channel registered with this name (see [Named Channels](../publishers/named-channels.md)) |
| *(null)* | The publisher uses its default channel-selection rules (all registered channels) |

---

## Common Use Cases

### Conditional Fan-Out to Multiple Channels

Re-publish the same event to a high-priority queue when the amount exceeds a threshold, and to a standard queue otherwise:

```csharp
pub.AddDispatcher()
   // High-value orders → priority queue
   .RouteToChannel(
       fb => fb.WithTypePattern("com.example.order.*")
               .WithJsonPath("amount", FilterOperator.GreaterThanOrEqual, "1000"),
       new EventPublishOptions { ChannelName = "priority-orders" })
   // All orders → standard queue (separate channel registration)
   .RouteToChannel(
       "com.example.order.*",
       new EventPublishOptions { ChannelName = "standard-orders" });
```

### Environment-Based Routing

Forward events to a different channel in non-production environments:

```csharp
if (!environment.IsProduction())
{
    pub.AddDispatcher()
       .RouteToChannel(
           "com.example.*",
           new EventPublishOptions { ChannelName = "dev-sink" },
           name: "dev-routing");
}
```

### Audit Trail for Sensitive Events

Re-publish payment events to a dedicated audit channel while normal processing continues:

```csharp
pub.AddDispatcher()
   .RouteToChannel(
       "com.example.payment.*",
       new EventPublishOptions { ChannelName = "audit-log" },
       name: "payment-audit");
```

---

## Implementing a Custom Routing Subscription

If you need routing behaviour beyond what `RouteToChannel` provides, implement `IRoutingEventSubscription` directly:

```csharp
public sealed class TenantRoutingSubscription : IRoutingEventSubscription
{
    private readonly IServiceProvider _services;
    private readonly ITenantChannelResolver _resolver;

    public TenantRoutingSubscription(IServiceProvider services, ITenantChannelResolver resolver)
    {
        _services = services;
        _resolver = resolver;
    }

    public string? Name => "tenant-router";

    public EventSubscriptionFilter Filter =>
        EventSubscriptionFilter.ForTypePattern("com.example.*");

    public EventPublishOptions? RoutingOptions => null; // resolved dynamically below

    public async Task HandleAsync(CloudEvent e, CancellationToken ct = default)
    {
        var tenantId = e["tenantid"]?.ToString();
        var channel  = await _resolver.ResolveChannelAsync(tenantId, ct);

        var publisher = _services.GetRequiredService<IEventPublisher>();
        await publisher.PublishEventAsync(
            e,
            new EventPublishOptions { ChannelName = channel },
            ct);
    }
}

// Registration:
pub.AddDispatcher()
   .Subscribe<TenantRoutingSubscription>();
```

