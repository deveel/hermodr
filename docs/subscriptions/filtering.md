# Subscription Filters

The `Hermodr.Subscriptions` package provides a composable filter system built on `FilterExpression` from the [`Deveel.Filters`](https://www.nuget.org/packages/Deveel.Filters) package. A `FilterExpression` can be evaluated against a `CloudEvent` using the `Matches` extension method:

```csharp
bool matches = filter.Matches(cloudEvent, EventSubscriptionContext.Empty);
```

The `EventFilter` static class contains factory methods that build `FilterExpression` instances targeting standard CloudEvents envelope attributes and JSON data-payload fields.

Alternatively, the `EventFilterBuilder` fluent API (obtained via `EventFilter.New()`) lets you compose the same filters in an imperative, chain-friendly style.

---

## `EventFilter` — Envelope Attribute Filters

### Type

```csharp
// Exact match
FilterExpression filter = EventFilter.ByType("com.example.order.placed");

// Wildcard match — trailing * = prefix, leading * = suffix
FilterExpression filter = EventFilter.ByTypePattern("com.example.*");
FilterExpression filter = EventFilter.ByTypePattern("*.placed");
```

### Source

```csharp
FilterExpression filter = EventFilter.BySource("https://api.example.com/orders");
FilterExpression filter = EventFilter.BySourcePattern("https://api.example.*");
```

### Subject

```csharp
FilterExpression filter = EventFilter.BySubject("order/42");
FilterExpression filter = EventFilter.BySubjectPattern("order/*");
```

### Extension Attributes

```csharp
// Matches events where the "tenantid" extension attribute equals "acme"
FilterExpression filter = EventFilter.ByExtension("tenantid", "acme");
```

---

## `EventFilter` — Data Payload Field Filters

Data field paths are dot-separated JSON paths (e.g. `"customer.tier"`). The prefix `data.` is added automatically.

> **JSON path validation**: each path segment may only contain letters, digits, and underscores (`_`). Hyphens and other special characters are **not** allowed. An `ArgumentException` is thrown for invalid paths.

### Equality

```csharp
// Exact string match
FilterExpression filter = EventFilter.ByField("customer.tier", "gold");

// Using explicit operator
FilterExpression filter = EventFilter.ByField("customer.tier", FilterExpressionType.Equal, "gold");
FilterExpression filter = EventFilter.ByField("payment.amount", FilterExpressionType.GreaterThan, 100.0);
FilterExpression filter = EventFilter.ByField("payment.isPaid", FilterExpressionType.Equal, true);
FilterExpression filter = EventFilter.ByField("order.createdAt", FilterExpressionType.GreaterThan,
    new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));
```

### Supported value types for `ByField`

`bool`, `int`, `long`, `double`, `string`, `DateTime`, `DateTimeOffset`

### `FilterExpressionType` values (numeric / datetime comparisons)

| Value | Description |
|-------|-------------|
| `Equal` | Exact equality |
| `NotEqual` | Not equal |
| `GreaterThan` | `>` |
| `GreaterThanOrEqual` | `>=` |
| `LessThan` | `<` |
| `LessThanOrEqual` | `<=` |

### String operations

```csharp
FilterExpression filter = EventFilter.FieldStartsWith("customer.name", "Acme");
FilterExpression filter = EventFilter.FieldEndsWith("order.reference", "-EU");
FilterExpression filter = EventFilter.FieldContains("order.status", "pending");
```

### Existence checks

```csharp
FilterExpression filter = EventFilter.FieldExists("customer.loyaltyCard");
FilterExpression filter = EventFilter.FieldNotExists("order.deletedAt");
```

---

## Combining Filters

Use `EventFilter.All` (AND) and `EventFilter.Any` (OR), or the lower-level `FilterExpression.And` / `FilterExpression.Or`:

```csharp
// AND — all must pass
FilterExpression filter = EventFilter.All(
    EventFilter.ByTypePattern("com.example.order.*"),
    EventFilter.ByField("customer.tier", "gold"));

// OR — at least one must pass
FilterExpression filter = EventFilter.Any(
    EventFilter.ByField("status", "placed"),
    EventFilter.ByField("status", "confirmed"));

// Nested: (tier == "gold" AND amount > 100) OR priority == "urgent"
FilterExpression filter = EventFilter.Any(
    EventFilter.All(
        EventFilter.ByField("customer.tier", "gold"),
        EventFilter.ByField("payment.amount", FilterExpressionType.GreaterThan, 100.0)),
    EventFilter.ByField("priority", "urgent"));
```

You may also use `FilterExpression.And` / `FilterExpression.Or` directly when combining exactly two expressions:

```csharp
FilterExpression filter =
    FilterExpression.And(
        EventFilter.ByType("com.example.order.placed"),
        EventFilter.ByField("customer.tier", "gold"));
```

---

## Fluent Builder — `EventFilterBuilder`

`EventFilter.New()` returns an `EventFilterBuilder` that lets you compose the same filters with a fluent chain. All conditions added at the top level are ANDed together. Use `AllOf`, `AnyOf`, and `Not` for explicit grouping.

### Basic chain (implicit AND)

```csharp
FilterExpression filter = EventFilter.New()
    .ByTypePattern("com.example.order.*")
    .ByExtension("tenantid", "acme")
    .WithField("customer.tier", "gold")
    .Build();
```

### OR group with `AnyOf`

```csharp
FilterExpression filter = EventFilter.New()
    .AnyOf(b => b
        .ByType("com.example.order.placed")
        .ByType("com.example.order.updated"))
    .Build();
```

### AND sub-group inside an OR with `AllOf`

```csharp
// (tier == "gold" AND amount > 100) OR priority == "urgent"
FilterExpression filter = EventFilter.New()
    .AnyOf(b => b
        .AllOf(inner => inner
            .WithField("customer.tier", "gold")
            .WithField("payment.amount", FilterExpressionType.GreaterThan, 100.0))
        .WithField("priority", "urgent"))
    .Build();
```

### Negation with `Not`

```csharp
// Exclude cancelled orders
FilterExpression filter = EventFilter.New()
    .ByTypePattern("com.example.order.*")
    .Not(b => b.WithField("status", "cancelled"))
    .Build();
```

### Builder method reference

| Method | Description |
|--------|-------------|
| `ByType(string)` | Exact `type` attribute match |
| `ByTypePattern(string)` | Wildcard `type` match (`*` prefix or suffix) |
| `BySource(string)` | Exact `source` match |
| `BySourcePattern(string)` | Wildcard `source` match |
| `BySubject(string)` | Exact `subject` match |
| `BySubjectPattern(string)` | Wildcard `subject` match |
| `ByExtension(string, string)` | Extension attribute equality |
| `WithField(string, string)` | Data field exact string match |
| `WithField(string, FilterExpressionType, T)` | Data field comparison (numeric, bool, datetime) |
| `FieldStartsWith(string, string)` | Data field starts-with |
| `FieldEndsWith(string, string)` | Data field ends-with |
| `FieldContains(string, string)` | Data field contains |
| `FieldExists(string)` | Data field presence check |
| `FieldNotExists(string)` | Data field absence check |
| `AllOf(Action<EventFilterBuilder>)` | Explicit AND sub-group |
| `AnyOf(Action<EventFilterBuilder>)` | OR sub-group |
| `Not(Action<EventFilterBuilder>)` | Negation of an AND sub-group |
| `Build()` | Produces the `FilterExpression`; returns `FilterExpression.Empty` when no conditions were added |

---

## Registering Subscriptions at Configuration Time

All `Subscribe` overloads on `EventPublisherBuilder` accept a `FilterExpression`:

```csharp
// 1. Type pattern shortcut (trailing * = prefix match)
builder.Subscribe("com.example.order.*", HandleOrder);

// 2. Pre-built FilterExpression
FilterExpression filter = EventFilter.ByType("com.example.invoice.issued");
builder.Subscribe(filter, HandleInvoice);

// 3. Combined attribute and data filters
builder.Subscribe(
    EventFilter.All(
        EventFilter.ByType("com.example.order.placed"),
        EventFilter.ByField("payment.amount", FilterExpressionType.GreaterThan, 500.0)),
    HandleHighValueOrder,
    name: "high-value-orders");

// 4. Using the fluent builder
builder.Subscribe(
    EventFilter.New()
        .ByTypePattern("com.example.order.*")
        .ByExtension("tenantid", "acme")
        .WithField("customer.tier", "gold")
        .Build(),
    HandleGoldTenantOrder,
    name: "gold-tenant-orders");
```

---

## Registering Subscriptions at Runtime

Inject `IEventSubscriptionRegistry` and call `RegisterAsync`:

```csharp
public class TenantSubscriptionService
{
    private readonly IEventSubscriptionRegistry _registry;

    public TenantSubscriptionService(IEventSubscriptionRegistry registry)
        => _registry = registry;

    public async Task AddOrderHandlerAsync(string tenantId, CancellationToken ct = default)
    {
        FilterExpression filter = EventFilter.New()
            .ByTypePattern("com.example.order.*")
            .ByExtension("tenantid", tenantId)
            .Build();

        var subscription = new EventSubscription(
            filter,
            (e, token) => ProcessOrderAsync(e, token),
            name: $"order-handler-{tenantId}");

        await _registry.RegisterAsync(subscription, ct);
    }
}
```

---

## Evaluating Filters Directly

Use the `Matches` extension method from `FilterExpressionExtensions` to test a filter outside the dispatcher pipeline:

```csharp
FilterExpression filter = EventFilter.All(
    EventFilter.ByType("com.example.order.placed"),
    EventFilter.ByField("payment.isPaid", FilterExpressionType.Equal, true));

bool matches = filter.Matches(cloudEvent, EventSubscriptionContext.Empty);
```

---

## Serializing Filters to JSON

Any `FilterExpression` can be round-tripped to and from JSON using `JsonFilterConverter` (from `Deveel.Filters`). Both methods accept an optional `JsonSerializerOptions` argument — if the converter is not already present it is injected automatically into a copy of the supplied options:

```csharp
// Serialize — default compact JSON
string json = filter.ToJson();

// Serialize — custom options (converter added to an internal copy automatically)
string pretty = filter.ToJson(new JsonSerializerOptions { WriteIndented = true });

// Deserialize
FilterExpression? restored = EventFilter.FromJson(json);

// Deserialize — custom options
FilterExpression? restored2 = EventFilter.FromJson(json,
    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
```

See [Filter Expressions — Serialization](filter-expressions.md#serialization) for full details, including property-level `[JsonConverter]` annotation and ASP.NET Core global registration.

