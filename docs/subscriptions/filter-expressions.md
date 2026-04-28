# Event Field Filters

`EventDataFilter` and `LogicalEventFilter` are `IEventFilter` implementations that navigate a dot-separated JSON path inside an event's data payload and compare the resolved value using a `FilterOperator` — all without delegates or lambdas, making them composable and portable.

## Motivation

Delegate-based filters are powerful but opaque — they cannot be introspected, serialized, or easily combined. `EventDataFilter` and `LogicalEventFilter` provide a structured, data-driven alternative that integrates naturally with the `IEventFilter` contract shared by all filter types, making them suitable for database-backed or dynamically generated subscription rules.

---

## `EventDataFilter`

Navigates a dot-separated path inside the JSON body and applies a `FilterOperator` comparison against a **typed** reference value.

### Supported value types

`bool`, `int`, `long`, `double`, `string`, `DateTime`, `DateTimeOffset`

For existence checks no reference value is needed — use the `Exists` / `NotExists` factory helpers instead.

### Factory methods

```csharp
// String comparison
EventDataFilter.Create("customer.tier", FilterOperator.Equals, "gold");
EventDataFilter.Create("customer.name", FilterOperator.StartsWith, "Acme");

// Numeric comparison
EventDataFilter.Create("payment.amount", FilterOperator.GreaterThan, 100.0);
EventDataFilter.Create("payment.amount", FilterOperator.LessThanOrEqual, 500.0);

// Boolean comparison
EventDataFilter.Create("payment.isPaid", FilterOperator.Equals, true);

// Date/time comparison
EventDataFilter.Create("order.createdAt", FilterOperator.GreaterThan, new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));

// Existence checks (no value required)
EventDataFilter.Exists("customer.loyaltyCard");
EventDataFilter.NotExists("order.deletedAt");
```

---

## `FilterOperator` Values

| Operator | Applies to | Description |
|----------|-----------|-------------|
| `Equals` | string, numeric, bool, datetime | Exact equality |
| `NotEquals` | string, numeric, bool, datetime | Not equal |
| `StartsWith` | string | Starts with the value |
| `EndsWith` | string | Ends with the value |
| `Contains` | string | Contains the value |
| `GreaterThan` | numeric, datetime | `>` |
| `LessThan` | numeric, datetime | `<` |
| `GreaterThanOrEqual` | numeric, datetime | `>=` |
| `LessThanOrEqual` | numeric, datetime | `<=` |
| `Exists` | any | The property exists (value is ignored) |
| `NotExists` | any | The property is absent (value is ignored) |

---

## `LogicalEventFilter`

Combines multiple `IEventFilter` instances with AND or OR logic.

```csharp
// AND — all must pass
var filter = LogicalEventFilter.And(
    EventDataFilter.Create("customer.tier", FilterOperator.Equals, "gold"),
    EventDataFilter.Create("payment.amount", FilterOperator.GreaterThan, 100.0));

// OR — at least one must pass
var filter = LogicalEventFilter.Or(
    EventDataFilter.Create("status", FilterOperator.Equals, "placed"),
    EventDataFilter.Create("status", FilterOperator.Equals, "confirmed"));

// Nested: (tier == "gold" AND amount > 100) OR priority == "urgent"
var filter = LogicalEventFilter.Or(
    LogicalEventFilter.And(
        EventDataFilter.Create("customer.tier", FilterOperator.Equals, "gold"),
        EventDataFilter.Create("payment.amount", FilterOperator.GreaterThan, 100.0)),
    EventDataFilter.Create("priority", FilterOperator.Equals, "urgent"));
```

An AND filter with zero children evaluates to `true`; an OR filter with zero children evaluates to `false`.

---

## Using Filters in a Subscription

Pass data filters directly to the `EventFilterBuilder` via `WithField` (single field) or `With` (any `IEventFilter`, including `LogicalEventFilter`):

```csharp
// Single field shorthand — exact string match
var filter = new EventFilterBuilder()
    .WithTypePattern("com.example.order.*")
    .WithField("customer.tier", "gold")
    .Build();

// Explicit operator
var filter = new EventFilterBuilder()
    .WithTypePattern("com.example.order.*")
    .WithField("customer.tier", FilterOperator.Equals, "gold")
    .Build();

// Logical combination
var filter = new EventFilterBuilder()
    .WithTypePattern("com.example.order.*")
    .With(LogicalEventFilter.And(
        EventDataFilter.Create("customer.tier", FilterOperator.Equals, "gold"),
        EventDataFilter.Create("payment.amount", FilterOperator.GreaterThan, 500.0)))
    .Build();
```

Or supply filters directly to `Subscribe`:

```csharp
builder.Subscribe(
    LogicalEventFilter.And(
        EventAttributeFilter.Type("com.example.order.placed"),
        EventDataFilter.Create("customer.tier", FilterOperator.Equals, "gold")),
    HandleGoldOrder,
    name: "gold-order-handler");
```

---

## Evaluating Directly

Every `IEventFilter` exposes `Matches(CloudEvent, EventSubscriptionContext)`:

```csharp
var filter = LogicalEventFilter.And(
    EventDataFilter.Create("customer.tier", FilterOperator.Equals, "gold"),
    EventDataFilter.Create("payment.isPaid", FilterOperator.Equals, true));

bool matches = filter.Matches(cloudEvent, EventSubscriptionContext.Empty);
```

---

## Combining with Attribute Filters

`EventAttributeFilter`, `EventDataFilter`, and `LogicalEventFilter` all implement `IEventFilter` and can be freely mixed:

```csharp
// Using EventFilterBuilder
var filter = new EventFilterBuilder()
    .WithType("com.example.order.placed")
    .WithField("customer.tier", FilterOperator.Equals, "gold")
    .With(LogicalEventFilter.Or(
        EventDataFilter.Create("payment.amount", FilterOperator.GreaterThan, 1000.0),
        EventDataFilter.Create("customer.vip", FilterOperator.Equals, true)))
    .Build();

// Or composing directly
var filter = LogicalEventFilter.And(
    EventAttributeFilter.Type("com.example.order.placed"),
    EventDataFilter.Create("customer.tier", FilterOperator.Equals, "gold"),
    LogicalEventFilter.Or(
        EventDataFilter.Create("payment.amount", FilterOperator.GreaterThan, 1000.0),
        EventDataFilter.Create("customer.vip", FilterOperator.Equals, true)));
```
