# Filter Expressions

Event subscription filters in `Deveel.Events.Subscriptions` are built on `FilterExpression` from the [`Deveel.Filters`](https://www.nuget.org/packages/Deveel.Filters) package. A `FilterExpression` is a structured, data-driven, serializable representation of a filter predicate — unlike a plain delegate, it can be introspected, persisted, and composed programmatically.

## Motivation

Delegate-based subscriptions are convenient but opaque — they cannot be persisted to a database, loaded from external configuration, or combined at runtime without custom glue code. `FilterExpression` solves this:

- **Composable** — `And`, `Or`, `Not` operations produce new expressions from existing ones.
- **Serializable** — the expression tree can be serialized to JSON (via `Deveel.Filters`) and stored in a database, configuration file, or remote store.
- **CloudEvent-aware** — the `CloudEventFilter` factory class maps CloudEvents envelope attributes and JSON data paths to `FilterExpression` variables that the built-in evaluator understands.

---

## Building Expressions with `CloudEventFilter`

### Envelope attributes

```csharp
// Exact type match
FilterExpression byType = CloudEventFilter.ByType("com.example.order.placed");

// Wildcard type match (trailing * = prefix, leading * = suffix)
FilterExpression byPattern = CloudEventFilter.ByTypePattern("com.example.*");

FilterExpression bySource = CloudEventFilter.BySource("https://api.example.com");
FilterExpression bySourcePattern = CloudEventFilter.BySourcePattern("https://api.example.*");

FilterExpression bySubject = CloudEventFilter.BySubject("order/42");
FilterExpression byExtension = CloudEventFilter.ByExtension("tenantid", "acme");
```

### Data payload fields

Paths are dot-separated JSON paths (e.g. `"customer.tier"`). The `data.` prefix is added automatically by the evaluator.

```csharp
// Exact string match on a JSON body field
FilterExpression byField = CloudEventFilter.ByField("customer.tier", "gold");

// Numeric comparison
FilterExpression byAmount = CloudEventFilter.ByField(
    "payment.amount", FilterExpressionType.GreaterThan, 100.0);

// Boolean field
FilterExpression byPaid = CloudEventFilter.ByField(
    "payment.isPaid", FilterExpressionType.Equal, true);

// Date/time comparison
FilterExpression byDate = CloudEventFilter.ByField(
    "order.createdAt", FilterExpressionType.GreaterThan,
    new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));

// String operations (not representable with FilterExpressionType)
FilterExpression startsWith = CloudEventFilter.FieldStartsWith("customer.name", "Acme");
FilterExpression endsWith   = CloudEventFilter.FieldEndsWith("order.ref", "-EU");
FilterExpression contains   = CloudEventFilter.FieldContains("order.status", "pending");

// Existence
FilterExpression exists    = CloudEventFilter.FieldExists("customer.loyaltyCard");
FilterExpression notExists = CloudEventFilter.FieldNotExists("order.deletedAt");
```

### Supported value types for `ByField`

`bool`, `int`, `long`, `double`, `string`, `DateTime`, `DateTimeOffset`

---

## `FilterExpressionType` Reference

| Value | Description | Applies to |
|-------|-------------|-----------|
| `Equal` | Exact equality | string, numeric, bool, datetime |
| `NotEqual` | Not equal | string, numeric, bool, datetime |
| `GreaterThan` | `>` | numeric, datetime |
| `GreaterThanOrEqual` | `>=` | numeric, datetime |
| `LessThan` | `<` | numeric, datetime |
| `LessThanOrEqual` | `<=` | numeric, datetime |

> **String operations** (StartsWith, EndsWith, Contains) and **existence checks** use dedicated `CloudEventFilter` factory methods rather than `FilterExpressionType`.

---

## Combining Expressions

`CloudEventFilter.All(…)` produces a logical AND over two or more expressions; `CloudEventFilter.Any(…)` produces a logical OR.

```csharp
// AND
FilterExpression filter = CloudEventFilter.All(
    CloudEventFilter.ByTypePattern("com.example.order.*"),
    CloudEventFilter.ByField("customer.tier", "gold"));

// OR
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

For exactly two operands you may use the lower-level `FilterExpression.And` / `FilterExpression.Or`:

```csharp
FilterExpression filter = FilterExpression.And(
    CloudEventFilter.ByType("com.example.order.placed"),
    CloudEventFilter.ByField("customer.tier", "gold"));
```

---

## Using Expressions in a Subscription

Pass any `FilterExpression` to `Subscribe`:

```csharp
// Type pattern shorthand (trailing * = prefix match)
builder.Subscribe("com.example.order.*", HandleOrder);

// Pre-built expression
builder.Subscribe(
    CloudEventFilter.All(
        CloudEventFilter.ByType("com.example.order.placed"),
        CloudEventFilter.ByField("customer.tier", "gold")),
    HandleGoldOrder,
    name: "gold-order-handler");
```

---

## Evaluating Expressions Directly

The `Matches` extension method (from `FilterExpressionExtensions`) evaluates any `FilterExpression` against a `CloudEvent`:

```csharp
FilterExpression filter = CloudEventFilter.All(
    CloudEventFilter.ByField("customer.tier", "gold"),
    CloudEventFilter.ByField("payment.isPaid", FilterExpressionType.Equal, true));

bool matches = filter.Matches(cloudEvent, EventSubscriptionContext.Empty);
```

---

## Serialization

`FilterExpression` instances can be serialized to and from JSON using the facilities provided by the `Deveel.Filters` package. This makes them suitable for storing subscription filters in a database or configuration file and reconstructing them at runtime — see [Custom Resolvers](custom-resolver.md) for a full example.
