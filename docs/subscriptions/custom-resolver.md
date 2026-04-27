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

```csharp
public sealed class DatabaseSubscriptionResolver : IEventSubscriptionResolver
{
    private readonly ISubscriptionRepository _db;

    public DatabaseSubscriptionResolver(ISubscriptionRepository db)
        => _db = db;

    public async Task<IReadOnlyList<IEventSubscription>> ResolveSubscriptionsAsync(
        CloudEvent @event,
        CancellationToken cancellationToken = default)
        => await ResolveSubscriptionsAsync(@event, context: null, cancellationToken);

    public async Task<IReadOnlyList<IEventSubscription>> ResolveSubscriptionsAsync(
        CloudEvent @event,
        EventSubscriptionContext? context,
        CancellationToken cancellationToken = default)
    {
        // Load all persisted filter models from the database
        var records = await _db.GetAllAsync(cancellationToken);

        var matched = new List<IEventSubscription>();

        foreach (var record in records)
        {
            // Deserialize the stored filter model
            var model  = EventSubscriptionFilterModel.FromJson(record.FilterJson)!;
            var filter = model.ToRuntimeFilter();

            // Check whether the filter matches the incoming event
            if (filter.Matches(@event, context?.Services))
            {
                matched.Add(new EventSubscription(
                    filter,
                    (e, ct) => InvokeHandlerAsync(record, e, ct),
                    name: record.Name));
            }
        }

        return matched;
    }

    private static Task InvokeHandlerAsync(
        SubscriptionRecord record,
        CloudEvent e,
        CancellationToken ct)
    {
        // Application-specific dispatch logic — e.g. call a webhook URL stored in the record
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

If you also want `IEventSubscriptionRegistry.RegisterAsync` to persist to the backing store:

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
        var filterModel = EventSubscriptionFilterModel.From(subscription.Filter);
        var record = new SubscriptionRecord
        {
            Name       = subscription.Name,
            FilterJson = filterModel.ToJson()
        };
        await _db.InsertAsync(record, cancellationToken);
    }
}
```

Register it as both interfaces so that `IEventSubscriptionRegistry` and `IEventSubscriptionResolver` both resolve the same singleton:

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

## `EventSubscriptionFilterModel` Reference

`EventSubscriptionFilterModel` is the bridge between the runtime filter and a serializable representation:

| Method | Description |
|--------|-------------|
| `EventSubscriptionFilterModel.From(filter)` | Converts a runtime `EventSubscriptionFilter` to a serializable model |
| `model.ToRuntimeFilter()` | Rebuilds a runtime `EventSubscriptionFilter` from the model |
| `model.ToJson(options?)` | Serializes the model to a JSON string |
| `EventSubscriptionFilterModel.FromJson(json, options?)` | Deserializes a model from a JSON string |
| `model.HasUnserializablePredicates` | `true` when the original filter contained a delegate that could not be serialized |

### What Is (and Is Not) Serializable

| Filter element | Serializable? | Notes |
|----------------|--------------|-------|
| `TypeFilter` / `SourceFilter` / `SubjectFilter` | ✅ Yes | Stored as `{ value, mode }` |
| `ExtensionFilters` | ✅ Yes | Dictionary of attribute name → `{ value, mode }` |
| `DataFilter` when it is a `JsonPathDataFilter` | ✅ Yes | Converted to a `JsonPathComparisonExpression` |
| `DataFilter` when it is a `JsonPredicateDataFilter` | ❌ No | Sets `HasUnserializablePredicates` |
| `DataFilter` when it is a `TypedDataFilter<T>` | ❌ No | Sets `HasUnserializablePredicates` |
| `Predicate` delegate | ❌ No | Sets `HasUnserializablePredicates` |

For body-filter persistence, model your filter using `FilterExpression` trees and assign them through `DataExpression`. See [Filter Expressions](filter-expressions.md) for details.

---

## Tips

- **Caching** — query the database once per dispatch is expensive at high throughput. Consider a short-lived cache (e.g. `MemoryCache` with a 30-second TTL) that is invalidated when new subscriptions are registered.
- **Lazy loading** — load subscriptions eagerly at startup and refresh periodically, rather than per-event, for large subscription sets.
- **Testing** — the resolver interface is straightforward to stub. See the [Testing](../testing/README.md) guide for patterns.


