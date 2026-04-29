# Event Dispatcher

`EventDispatcher` routes published `CloudEvent` instances to matching `IEventSubscription` handlers. It implements `IEventMiddleware` and runs inside the `EventPublisher` middleware pipeline.

## Registration and Pipeline Activation

Subscription support now has two distinct steps:

1. **Register subscription services** at startup with `AddSubscriptions()`.
2. **Enable dispatch middleware** on the runtime publisher with one of the `UseDispatcher(...)` extensions.

```csharp
// Startup / DI registration
services.AddEventPublisher()
    .AddSubscriptions()
    .Subscribe("com.example.order.*", async (e, ct) =>
    {
        Console.WriteLine($"Received: {e.Type}");
        await Task.CompletedTask;
    }, name: "log-orders");

// Runtime activation (once per publisher instance)
var publisher = serviceProvider.GetRequiredService<EventPublisher>()
    .UseDispatcher();
```

`AddSubscriptions()` registers:

| Service | Lifetime | Description |
|---------|----------|-------------|
| `EventSubscriptionRegistry` | Singleton | In-memory registry seeded from `IEventSubscription` instances registered in DI |
| `IEventSubscriptionRegistry` | Singleton | Write interface for runtime registration |
| `IEventSubscriptionResolver` | Singleton | Read interface resolved from the same registry instance |

> **Tip:** register `AddSubscriptions()` during startup before the first `UseDispatcher()`/publish call so the resolver services are available when middleware runs.

---

## Runtime Pipeline Functions

Use one of these extension methods on `EventPublisher`:

> `EventDispatcher` is an internal implementation detail; enable it through `UseDispatcher(...)` rather than instantiating it directly.

| Function | Description |
|----------|-------------|
| `UseDispatcher()` | Adds dispatcher middleware with default options |
| `UseDispatcher(EventDispatcherOptions options)` | Adds dispatcher middleware with explicit runtime options |

### `UseDispatcher()`

```csharp
var publisher = serviceProvider.GetRequiredService<EventPublisher>()
    .UseDispatcher();
```

### `UseDispatcher(EventDispatcherOptions)`

```csharp
var publisher = serviceProvider.GetRequiredService<EventPublisher>()
    .UseDispatcher(new EventDispatcherOptions
    {
        ThrowOnHandlerError = true
    });
```

| Option | Default | Description |
|--------|---------|-------------|
| `ThrowOnHandlerError` | `false` | When `true`, a subscription handler exception is re-thrown and dispatching stops. When `false`, the exception is logged and dispatching continues with remaining matches. |

---

## How Dispatch Works

```
EventPublisher.Publish*()
  │
  ├── ...middleware before dispatcher...
  │
  ├── EventDispatcher.InvokeAsync(context, next)
  │     ├── Build EventSubscriptionContext from context.Services
  │     ├── For each IEventSubscriptionResolver:
  │     │     └── ResolveSubscriptionsAsync(event, context)
  │     ├── Aggregate matches from all resolvers
  │     ├── Invoke each subscription.HandleAsync(event, ct)
  │     └── On error: log + rethrow only when ThrowOnHandlerError = true
  │
  └── Continue with next middleware/terminal delegate
```

---

## Multiple Resolvers

The dispatcher queries **all** registered `IEventSubscriptionResolver` instances. You can combine:

- The default in-memory `EventSubscriptionRegistry`
- Custom resolvers (database, remote API, etc.)

```csharp
pub.AddSubscriptions()
   .AddSubscriptionResolver<DatabaseSubscriptionResolver>();
```

See [Custom Resolvers](custom-resolver.md) for implementation details.

---

## Class-Based Subscriptions

For subscriptions that need injected services, implement `IEventSubscription` directly:

```csharp
public sealed class AuditOrderSubscription : IEventSubscription
{
    private readonly IAuditService _audit;

    public AuditOrderSubscription(IAuditService audit)
        => _audit = audit;

    public string? Name => "audit-orders";

    public FilterExpression Filter =>
        EventFilter.ByTypePattern("com.example.order.*");

    public Task HandleAsync(CloudEvent e, CancellationToken ct = default)
        => _audit.RecordAsync(e, ct);
}

pub.AddSubscriptions()
   .Subscribe<AuditOrderSubscription>();
```

---

## Runtime Subscription Registration

Subscriptions can be added after startup by using `IEventSubscriptionRegistry`:

```csharp
public class WebhookManager
{
    private readonly IEventSubscriptionRegistry _registry;

    public WebhookManager(IEventSubscriptionRegistry registry)
        => _registry = registry;

    public async Task AddWebhookAsync(string tenantId, string webhookUrl, CancellationToken ct = default)
    {
        var filter = EventFilter.All(
            EventFilter.ByTypePattern("com.example.*"),
            EventFilter.ByExtension("tenantid", tenantId));

        var subscription = new EventSubscription(
            filter,
            async (e, token) =>
            {
                using var client = new HttpClient();
                await client.PostAsJsonAsync(webhookUrl, e, token);
            },
            name: $"webhook-{tenantId}");

        await _registry.RegisterAsync(subscription, ct);
    }
}
```

> **Note:** `EventSubscriptionRegistry` is in-memory and thread-safe; runtime registrations are not persisted across process restarts.

---

## Custom Data Deserialization

When a `data.*` filter is evaluated, `EventFilterEvaluator` calls `EventSubscriptionContext.GetJsonData(event)`. The context first tries DI-registered `IEventDataDeserializer` implementations (matching by `datacontenttype`) and falls back to the built-in JSON deserializer.

```csharp
services.AddSingleton<IEventDataDeserializer, MyCustomDeserializer>();
```

