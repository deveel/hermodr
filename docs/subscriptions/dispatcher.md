# Event Dispatcher

`EventDispatcher` is the engine that routes every published `CloudEvent` to the matching registered subscriptions. It implements both `IEventDispatcher` (for direct dispatch) and `IEventPublishChannel` (so it plugs into the standard publisher fan-out pipeline).

## Registration

Call `AddDispatcher()` on the `EventPublisherBuilder` in your DI setup:

```csharp
services.AddEventPublisher(pub =>
{
    pub.AddDispatcher();
    // ... add channels, subscriptions, etc.
});
```

`AddDispatcher()` registers:

| Service | Lifetime | Description |
|---------|----------|-------------|
| `EventSubscriptionRegistry` | Singleton | In-memory registry (write + read), pre-populated from any `IEventSubscription` instances registered with DI |
| `IEventSubscriptionRegistry` | Singleton | Write interface for runtime subscription registration |
| `IEventSubscriptionResolver` | Singleton | Read interface forwarded to the in-memory registry; queried by the dispatcher alongside custom resolvers |
| `EventDispatcher` | Singleton | The dispatcher itself |
| `IEventDispatcher` | Singleton | Public dispatch interface |
| `IEventPublishChannel` | Singleton | Wired into the publisher's fan-out pipeline |

> **Order matters:** call `AddDispatcher()` **before** any `Subscribe(…)` calls so that the `IEventSubscription` instances registered in DI are picked up when the registry singleton is first built.

---

## Dispatcher Options

Configure `EventDispatcherOptions` through the optional `configure` parameter:

```csharp
pub.AddDispatcher(options =>
{
    options.ThrowOnHandlerError = true;  // default: false
});
```

| Option | Default | Description |
|--------|---------|-------------|
| `ThrowOnHandlerError` | `false` | When `true`, an exception in any handler propagates to the caller and stops dispatching to subsequent subscribers. When `false`, the exception is logged and dispatching continues with the next matched subscription. |

The `false` default ensures that a single misbehaving subscriber cannot prevent other subscribers from processing the event.

---

## How Dispatch Works

```
DispatchAsync(event)
  │
  ├── Build EventSubscriptionContext (wraps IServiceProvider)
  │
  ├── For each IEventSubscriptionResolver:
  │     └── ResolveSubscriptionsAsync(event, context)
  │           └── Returns IReadOnlyList<IEventSubscription>
  │
  ├── Aggregate all matches across resolvers
  │
  └── For each matched subscription:
        ├── Log "dispatching"
        ├── await subscription.HandleAsync(event, ct)
        ├── Log "dispatched"  (success)
        └── On exception:
              ├── Log error
              └── If ThrowOnHandlerError → rethrow (stops loop)
                  else → continue
```

---

## Multiple Resolvers

The dispatcher aggregates results from **all** registered `IEventSubscriptionResolver` instances. This lets you combine:

- The built-in `EventSubscriptionRegistry` (always registered by `AddDispatcher`, default in-memory store)
- One or more custom resolvers (e.g. reading subscriptions from a database)

```csharp
pub.AddDispatcher()
   .AddSubscriptionResolver<DatabaseSubscriptionResolver>();
```

See [Custom Resolvers](custom-resolver.md) for implementation details.

---

## Using `IEventDispatcher` Directly

If you need to dispatch an event without going through `IEventPublisher`, inject `IEventDispatcher`:

```csharp
public class MyBackgroundService : BackgroundService
{
    private readonly IEventDispatcher _dispatcher;

    public MyBackgroundService(IEventDispatcher dispatcher)
        => _dispatcher = dispatcher;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var cloudEvent = new CloudEvent
        {
            Id     = Guid.NewGuid().ToString(),
            Type   = "com.example.heartbeat",
            Source = new Uri("https://background/service")
        };

        await _dispatcher.DispatchAsync(cloudEvent, stoppingToken);
    }
}
```

---

## Class-Based Subscriptions

For subscriptions that require DI-injected services, implement `IEventSubscription` directly:

```csharp
public sealed class AuditOrderSubscription : IEventSubscription
{
    private readonly IAuditService _audit;

    public AuditOrderSubscription(IAuditService audit)
        => _audit = audit;

    public string? Name => "audit-orders";

    public IEventFilter Filter =>
        EventAttributeFilter.Type("com.example.order.*", parseWildcard: true);

    public Task HandleAsync(CloudEvent e, CancellationToken ct = default)
        => _audit.RecordAsync(e, ct);
}

// Registration:
pub.AddDispatcher()
   .Subscribe<AuditOrderSubscription>();
```

The type is registered with the DI container and resolved when the registry is first built, so constructor injection of any scoped or singleton service is fully supported.

---

## Runtime Subscription Registration

Subscriptions can also be added after the application has started by injecting `IEventSubscriptionRegistry`:

```csharp
public class WebhookManager
{
    private readonly IEventSubscriptionRegistry _registry;

    public WebhookManager(IEventSubscriptionRegistry registry)
        => _registry = registry;

    public async Task AddWebhookAsync(
        string tenantId,
        string webhookUrl,
        CancellationToken ct = default)
    {
        var filter = LogicalEventFilter.And(
            EventAttributeFilter.Type("com.example.*", parseWildcard: true),
            EventAttributeFilter.ForExtension("tenantid", tenantId));

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

> **Note:** The built-in `EventSubscriptionRegistry` is an in-memory, thread-safe store — subscriptions registered at runtime are **not** persisted across application restarts. For subscriptions that must survive restarts or be managed externally, implement a [custom resolver backed by a database or remote service](custom-resolver.md).

---

## Custom Data Deserialization

When an `EventDataFilter` evaluates the event payload, it calls `EventSubscriptionContext.GetJsonData(event)` internally. The context first checks whether any `IEventDataDeserializer` registered with the DI container can handle the event's `datacontenttype`; if none matches, it falls back to the built-in JSON deserializer which handles JSON strings, `JsonElement` objects, and CLR objects serializable with `System.Text.Json`.

Register a custom deserializer:

```csharp
services.AddSingleton<IEventDataDeserializer, MyCustomDeserializer>();
```

The `CanDeserialize(string? contentType)` method determines whether your deserializer is selected for a given content type.
