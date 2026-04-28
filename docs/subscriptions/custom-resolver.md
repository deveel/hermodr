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
            // Reconstruct a FilterExpression from the stored representation.
            FilterExpression filter = BuildFilterFromRecord(record);

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

    private static FilterExpression BuildFilterFromRecord(SubscriptionRecord record)
    {
        // Example: reconstruct the FilterExpression from stored type/field criteria.
        // The filter expression tree can be serialized to/from JSON using Deveel.Filters,
        // so you can store the raw JSON in a column and deserialize it here.
        // Alternatively, reconstruct it from individual fields:
        var filters = new List<FilterExpression>();

        if (!string.IsNullOrEmpty(record.TypePattern))
            filters.Add(CloudEventFilter.ByTypePattern(record.TypePattern));

        foreach (var field in record.FieldFilters)
            filters.Add(CloudEventFilter.ByField(field.Path, field.Value));

        return filters.Count switch
        {
            0 => FilterExpression.Empty,
            1 => filters[0],
            _ => CloudEventFilter.All(filters.ToArray())
        };
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
        // Persist the filter to the database. Because FilterExpression is serializable
        // (via Deveel.Filters) you can store the entire expression tree as JSON:
        //   var json = JsonSerializer.Serialize(subscription.Filter, filterJsonOptions);
        //
        // Or extract individual fields from the expression — adapt as needed:
        var record = new SubscriptionRecord
        {
            Name        = subscription.Name,
            FilterJson  = SerializeFilter(subscription.Filter),
            // ... other fields ...
        };
        await _db.InsertAsync(record, cancellationToken);
    }

    private static string SerializeFilter(FilterExpression filter)
    {
        // Use the Deveel.Filters JSON serializer — shown here as a placeholder.
        return System.Text.Json.JsonSerializer.Serialize(filter);
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

The context overload passes the application `IServiceProvider` through to the built-in `CloudEventFilterEvaluator`, which calls `context.GetJsonData(event)` when resolving `data.*` variable paths. This allows DI-registered `IEventDataDeserializer` services to handle custom content types. Always prefer the context overload; the no-context overload is provided for backward compatibility.

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
