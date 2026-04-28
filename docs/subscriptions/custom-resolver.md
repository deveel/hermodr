# Custom Subscription Resolvers

The built-in `EventSubscriptionRegistry` keeps subscriptions in memory. For scenarios where subscriptions must **survive application restarts** or be managed by an external system, implement a custom `IEventSubscriptionResolver` backed by a database, remote API, or any other store.

## Interfaces

| Interface | Responsibility |
|-----------|---------------|
| `IEventSubscriptionResolver` | **Read-only** — resolves matching subscriptions for a dispatched event |
| `IEventSubscriptionRegistry` | **Read + write** — extends the resolver with `RegisterAsync` for runtime additions |

Implement `IEventSubscriptionResolver` for read-only sources (remote service, read-only DB view, configuration file). Implement `IEventSubscriptionRegistry` only when your backing store supports writes.

---

## Implementing a Read-Only Resolver

A resolver receives the incoming `CloudEvent` and an optional `EventSubscriptionContext` (which carries the application `IServiceProvider`) and must return all subscriptions whose filter matches the event.

```csharp
public sealed class DatabaseSubscriptionResolver : IEventSubscriptionResolver
{
    private readonly ISubscriptionRepository _db;

    public DatabaseSubscriptionResolver(ISubscriptionRepository db)
        => _db = db;

    public Task<IReadOnlyList<IEventSubscription>> ResolveSubscriptionsAsync(
        CloudEvent @event,
        CancellationToken cancellationToken = default)
        => ResolveSubscriptionsAsync(@event, context: null, cancellationToken);

    public async Task<IReadOnlyList<IEventSubscription>> ResolveSubscriptionsAsync(
        CloudEvent @event,
        EventSubscriptionContext? context,
        CancellationToken cancellationToken = default)
    {
        // Load all persisted filter records from the database.
        var records = await _db.GetAllAsync(cancellationToken);

        var ctx = context ?? EventSubscriptionContext.Empty;
        var matched = new List<IEventSubscription>();

        foreach (var record in records)
        {
            // Reconstruct an IEventFilter from the stored representation.
            IEventFilter filter = BuildFilterFromRecord(record);

            if (filter.Matches(@event, ctx))
            {
                matched.Add(new EventSubscription(
                    filter,
                    (e, ct) => InvokeHandlerAsync(record, e, ct),
                    name: record.Name));
            }
        }

        return matched;
    }

    private static IEventFilter BuildFilterFromRecord(SubscriptionRecord record)
    {
        // Example: reconstruct the filter from stored type/field criteria.
        // Adapt this to however your records store filter information.
        var builder = new EventFilterBuilder();

        if (!string.IsNullOrEmpty(record.TypePattern))
            builder.WithTypePattern(record.TypePattern);

        foreach (var field in record.FieldFilters)
            builder.WithField(field.Path, field.Operator, field.Value);

        return builder.Build();
    }

    private static Task InvokeHandlerAsync(
        SubscriptionRecord record,
        CloudEvent e,
        CancellationToken ct)
    {
        // Application-specific dispatch logic — e.g. call a webhook URL stored in the record.
        throw new NotImplementedException("Replace with real handler logic");
    }
}
```

### Registering the Resolver

```csharp
pub.AddDispatcher()
   .AddSubscriptionResolver<DatabaseSubscriptionResolver>();
```

The dispatcher will query **both** the built-in `EventSubscriptionRegistry` **and** your custom resolver. Matches from all resolvers are aggregated before handlers are invoked.

By default the resolver is registered as a singleton. Pass a different lifetime if needed:

```csharp
pub.AddDispatcher()
   .AddSubscriptionResolver<DatabaseSubscriptionResolver>(ServiceLifetime.Scoped);
```

---

## Implementing a Writable Registry

If you also want `IEventSubscriptionRegistry.RegisterAsync` to persist to the backing store, implement both interfaces:

```csharp
public sealed class DatabaseSubscriptionRegistry :
    DatabaseSubscriptionResolver,
    IEventSubscriptionRegistry
{
    private readonly ISubscriptionRepository _db;

    public DatabaseSubscriptionRegistry(ISubscriptionRepository db) : base(db)
        => _db = db;

    public async Task RegisterAsync(
        IEventSubscription subscription,
        CancellationToken cancellationToken = default)
    {
        // Persist the filter criteria to the database. The exact serialization
        // format is application-specific — for example, store the type pattern
        // and field filters as JSON columns in a subscriptions table.
        var record = new SubscriptionRecord
        {
            Name        = subscription.Name,
            TypePattern = ExtractTypePattern(subscription.Filter),
            // ... other serialized fields ...
        };
        await _db.InsertAsync(record, cancellationToken);
    }

    private static string? ExtractTypePattern(IEventFilter filter)
    {
        // Inspect the filter tree to extract the type pattern — adapt as needed.
        if (filter is EventAttributeFilter attrFilter &&
            string.Equals(attrFilter.AttributeName, "type", StringComparison.OrdinalIgnoreCase))
        {
            return attrFilter.MatchMode switch
            {
                FilterMatchMode.Prefix => attrFilter.Value + "*",
                FilterMatchMode.Suffix => "*" + attrFilter.Value,
                _                      => attrFilter.Value
            };
        }
        return null;
    }
}
```

Register it as both interfaces so that `IEventSubscriptionRegistry` and `IEventSubscriptionResolver` resolve the same singleton:

```csharp
services.AddSingleton<DatabaseSubscriptionRegistry>();
services.AddSingleton<IEventSubscriptionRegistry>(sp =>
    sp.GetRequiredService<DatabaseSubscriptionRegistry>());
services.AddSingleton<IEventSubscriptionResolver>(sp =>
    sp.GetRequiredService<DatabaseSubscriptionRegistry>());

// NOTE: do NOT call AddSubscriptionResolver<T>() here — that would register
// a second IEventSubscriptionResolver independent of the registry singleton.
```

---

## Key Contracts

### `IEventSubscriptionResolver`

```csharp
Task<IReadOnlyList<IEventSubscription>> ResolveSubscriptionsAsync(
    CloudEvent @event,
    CancellationToken cancellationToken = default);

Task<IReadOnlyList<IEventSubscription>> ResolveSubscriptionsAsync(
    CloudEvent @event,
    EventSubscriptionContext? context,
    CancellationToken cancellationToken = default);
```

The context overload passes the application `IServiceProvider` through to DI-aware filters (e.g. `EventDataFilter` uses `context.GetJsonData(event)` which can resolve custom `IEventDataDeserializer` services). Always prefer the context overload; the no-context overload is provided for backward compatibility.

### `EventSubscriptionContext`

| Member | Description |
|--------|-------------|
| `EventSubscriptionContext.Empty` | Shared sentinel with no service provider — safe to use when no DI context is available |
| `Services` | The `IServiceProvider`, or `null` when the context is empty |
| `GetJsonData(CloudEvent)` | Returns the event payload as a `JsonElement?`, using DI-registered `IEventDataDeserializer` instances (falls back to the built-in JSON deserializer) |

---

## Tips

- **Caching** — querying the database once per dispatch is expensive at high throughput. Consider a short-lived cache (e.g. `IMemoryCache` with a 30-second TTL) that is invalidated when new subscriptions are registered.
- **Lazy loading** — load subscriptions eagerly at startup and refresh periodically, rather than per-event, for large subscription sets.
- **Testing** — the resolver interface is straightforward to stub or mock. See the [Testing](../testing/README.md) guide for patterns.
