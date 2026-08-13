# Event Dispatcher

`EventDispatcher` routes published `CloudEvent` instances to matching `IEventSubscription` handlers. It implements `IEventMiddleware` and runs inside the `EventPublisher` middleware pipeline.

## Registration and Pipeline Activation

Subscription support is configured entirely at startup:

1. **Register subscription services** with `AddSubscriptions()`.
2. **Configure dispatcher options** through the `AddSubscriptions(...)` overload or `EventSubscriptionsBuilder.ConfigureOptions(...)`.
3. **Publish events normally** — the dispatcher middleware is already part of the pipeline.

```csharp
// Startup / DI registration
services.AddEventPublisher()
    .AddSubscriptions(subs =>
    {
        subs.ConfigureOptions(options => options.ThrowOnHandlerError = true);
        subs.Subscribe("com.example.order.*", async (e, ct) =>
        {
            Console.WriteLine($"Received: {e.Type}");
            await Task.CompletedTask;
        }, name: "log-orders");
    });

var publisher = serviceProvider.GetRequiredService<IEventPublisher>();
```

`AddSubscriptions()` registers:

| Service | Lifetime | Description |
|---------|----------|-------------|
| `EventSubscriptionRegistry` | Singleton | In-memory registry seeded from `IEventSubscription` instances registered in DI |
| `IEventSubscriptionRegistry` | Singleton | Write interface for runtime registration |
| `IEventSubscriptionResolver` | Singleton | Read interface resolved from the same registry instance |

It also appends `EventDispatcher` to the publisher pipeline immediately. No runtime activation call is required.

---

## Compatibility `UseDispatcher()` extensions

The `UseDispatcher()` extensions on `EventPublisher` are retained only for source compatibility with older code. They **return the publisher unchanged** and do not modify the pipeline.

| Function | Current behavior |
|----------|------------------|
| `UseDispatcher()` | No-op; dispatcher is already wired by `AddSubscriptions()` |
| `UseDispatcher(EventDispatcherOptions options)` | No-op; `options` are ignored |

Configure dispatcher behavior during DI registration instead:

```csharp
services.AddEventPublisher()
    .AddSubscriptions(subs =>
    {
        subs.ConfigureOptions(options => options.ThrowOnHandlerError = true);
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
