# Subscription Filters

The `Deveel.Events.Subscriptions` package provides a composable filter system built on `FilterExpression` from the [`Deveel.Filters`](https://www.nuget.org/packages/Deveel.Filters) package. A `FilterExpression` can be evaluated against a `CloudEvent` using the `Matches` extension method:

```csharp
bool matches = filter.Matches(cloudEvent, EventSubscriptionContext.Empty);
```

The `CloudEventFilter` static class contains factory methods that build `FilterExpression` instances targeting standard CloudEvents envelope attributes and JSON data-payload fields.

---

## `CloudEventFilter` — Envelope Attribute Filters

### Type

```csharp
// Exact match
FilterExpression filter = CloudEventFilter.ByType("com.example.order.placed");

// Wildcard match — trailing * = prefix, leading * = suffix
FilterExpression filter = CloudEventFilter.ByTypePattern("com.example.*");
FilterExpression filter = CloudEventFilter.ByTypePattern("*.placed");
```

### Source

```csharp
FilterExpression filter = CloudEventFilter.BySource("https://api.example.com/orders");
FilterExpression filter = CloudEventFilter.BySourcePattern("https://api.example.*");
```

### Subject

```csharp
FilterExpression filter = CloudEventFilter.BySubject("order/42");
FilterExpression filter = CloudEventFilter.BySubjectPattern("order/*");
```

### Extension Attributes

```csharp
// Matches events where the "tenantid" extension attribute equals "acme"
FilterExpression filter = CloudEventFilter.ByExtension("tenantid", "acme");
```

---

## `CloudEventFilter` — Data Payload Field Filters

Data field paths are dot-separated JSON paths (e.g. `"customer.tier"`). The prefix `data.` is added automatically.

### Equality

```csharp
// Exact string match
FilterExpression filter = CloudEventFilter.ByField("customer.tier", "gold");

// Using explicit operator
FilterExpression filter = CloudEventFilter.ByField("customer.tier", FilterExpressionType.Equal, "gold");
FilterExpression filter = CloudEventFilter.ByField("payment.amount", FilterExpressionType.GreaterThan, 100.0);
FilterExpression filter = CloudEventFilter.ByField("payment.isPaid", FilterExpressionType.Equal, true);
FilterExpression filter = CloudEventFilter.ByField("order.createdAt", FilterExpressionType.GreaterThan,
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
FilterExpression filter = CloudEventFilter.FieldStartsWith("customer.name", "Acme");
FilterExpression filter = CloudEventFilter.FieldEndsWith("order.reference", "-EU");
FilterExpression filter = CloudEventFilter.FieldContains("order.status", "pending");
```

### Existence checks

```csharp
FilterExpression filter = CloudEventFilter.FieldExists("customer.loyaltyCard");
FilterExpression filter = CloudEventFilter.FieldNotExists("order.deletedAt");
```

---

## Combining Filters

Use `CloudEventFilter.All` (AND) and `CloudEventFilter.Any` (OR), or the lower-level `FilterExpression.And` / `FilterExpression.Or`:

```csharp
// AND — all must pass
FilterExpression filter = CloudEventFilter.All(
    CloudEventFilter.ByTypePattern("com.example.order.*"),
    CloudEventFilter.ByField("customer.tier", "gold"));

// OR — at least one must pass
FilterExpression filter = CloudEventFilter.Any(
    CloudEventFilter.ByField("status", "placed"),
    CloudEventFilter.ByField("status", "confirmed"));

// Nested: (tier == "gold" AND amount > 100) OR priority == "urgent"
FilterExpression filter = CloudEventFilter.Any(
    CloudEventFilter.All(
        CloudEventFilter.ByField("customer.tier", "gold"),
        CloudEventFilter.ByField("payment.amount", FilterExpressionType.GreaterThan, 100.0)),
    CloudEventFilter.ByField("priority", "urgent"));
```

You may also use `FilterExpression.And` / `FilterExpression.Or` directly when combining exactly two expressions:

```csharp
FilterExpression filter =
    FilterExpression.And(
        CloudEventFilter.ByType("com.example.order.placed"),
        CloudEventFilter.ByField("customer.tier", "gold"));
```

---

## Registering Subscriptions at Configuration Time

All `Subscribe` overloads on `EventPublisherBuilder` accept a `FilterExpression`:

```csharp
// 1. Type pattern shortcut (trailing * = prefix match)
builder.Subscribe("com.example.order.*", HandleOrder);

// 2. Pre-built FilterExpression
FilterExpression filter = CloudEventFilter.ByType("com.example.invoice.issued");
builder.Subscribe(filter, HandleInvoice);

// 3. Combined attribute and data filters
builder.Subscribe(
    CloudEventFilter.All(
        CloudEventFilter.ByType("com.example.order.placed"),
        CloudEventFilter.ByField("payment.amount", FilterExpressionType.GreaterThan, 500.0)),
    HandleHighValueOrder,
    name: "high-value-orders");
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
        FilterExpression filter = CloudEventFilter.All(
            CloudEventFilter.ByTypePattern("com.example.order.*"),
            CloudEventFilter.ByExtension("tenantid", tenantId));

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
FilterExpression filter = CloudEventFilter.All(
    CloudEventFilter.ByType("com.example.order.placed"),
    CloudEventFilter.ByField("payment.isPaid", FilterExpressionType.Equal, true));

bool matches = filter.Matches(cloudEvent, EventSubscriptionContext.Empty);
```
