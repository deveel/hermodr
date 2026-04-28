# Subscription Filters

The `Deveel.Events.Subscriptions` package provides a rich, composable filter system built on the `IEventFilter` interface. Every filter implements a single method:

```csharp
bool Matches(CloudEvent @event, EventSubscriptionContext context);
```

Three concrete implementations ship out of the box:

| Type | Inspects | Description |
|------|----------|-------------|
| `EventAttributeFilter` | CloudEvents envelope | Matches a named envelope attribute (type, source, subject, id, time, datacontenttype, dataschema, or any extension attribute) |
| `EventDataFilter` | JSON data payload | Navigates a dot-separated path inside the JSON body and compares the value using a `FilterOperator` |
| `LogicalEventFilter` | Multiple child filters | Combines other filters with AND or OR logic |

---

## `EventAttributeFilter`

Matches a named CloudEvents envelope attribute against a value using a `FilterMatchMode` strategy.

### Match modes

| `FilterMatchMode` | Description | Example pattern | Matches |
|-------------------|-------------|-----------------|---------|
| `Exact` (default) | Ordinal, case-sensitive equality | `"com.example.order.placed"` | Exact string only |
| `Prefix` | Attribute starts with the value | `"com.example."` | Anything starting with the literal |
| `Suffix` | Attribute ends with the value | `".placed"` | Anything ending with the literal |

Trailing `*` in a pattern string → `Prefix`; leading `*` → `Suffix`; no wildcard → `Exact` (via `parseWildcard: true`).

### Standard attributes

The supported standard attribute names are: `type`, `source`, `subject`, `id`, `time`, `datacontenttype`, `dataschema`.

Extension attributes must be prefixed with `extension.` (e.g. `"extension.tenantid"`).

### Factory methods

```csharp
// Type attribute — exact match
EventAttributeFilter.Type("com.example.order.placed");

// Type attribute — pattern match (trailing * = prefix, leading * = suffix)
EventAttributeFilter.Type("com.example.*", parseWildcard: true);

// General attribute — exact match
EventAttributeFilter.For("source", "https://api.example.com/orders");

// General attribute — pattern match
EventAttributeFilter.For("source", "https://api.example.*", parseWildcard: true);

// Extension attribute (the "extension." prefix is added automatically)
EventAttributeFilter.ForExtension("tenantid", "acme");
EventAttributeFilter.ForExtension("tenantid", "acme", FilterMatchMode.Exact);
```

### Value-only matching

`EventAttributeFilter` also exposes a `Matches(string? input)` overload to test a plain string without a `CloudEvent`:

```csharp
var filter = new EventAttributeFilter("type", "com.example.", FilterMatchMode.Prefix);
bool ok = filter.Matches("com.example.order.placed"); // true
```

---

## `EventFilterBuilder`

A fluent builder that composes multiple filters into a single `LogicalEventFilter.And(…)`. All criteria must pass for the overall filter to match.

```csharp
var filter = new EventFilterBuilder()
    .WithType("com.example.order.placed")          // exact type match
    .WithSourcePattern("https://orders.*")          // source prefix match
    .WithSubjectPattern("*.vip")                    // subject suffix match
    .Build();
```

### Available methods

| Method | Filter added |
|--------|-------------|
| `WithType(type)` | `EventAttributeFilter.Type(type)` — exact `type` attribute match |
| `WithTypePattern(pattern)` | `EventAttributeFilter.Type(pattern, parseWildcard: true)` — wildcard `type` match |
| `WithSource(source)` | `EventAttributeFilter("source", source)` — exact `source` match |
| `WithSourcePattern(pattern)` | Wildcard `source` match |
| `WithSubject(subject)` | Exact `subject` match |
| `WithSubjectPattern(pattern)` | Wildcard `subject` match |
| `WithField(path, value)` | `EventDataFilter.Create(path, Equals, value)` — exact string match on a JSON body field |
| `WithField(path, operator, value)` | `EventDataFilter.Create(path, operator, value)` — typed comparison on a JSON body field |
| `With(filter)` | Adds any `IEventFilter` directly (including `LogicalEventFilter`) |
| `Build()` | Returns `LogicalEventFilter.And(…all added filters…)` |

`Build()` on an empty builder returns a filter that matches every event.

---

## `EventDataFilter`

Navigates a dot-separated path inside the JSON body and applies a `FilterOperator` comparison against a **typed** reference value.

### Supported value types

`bool`, `int`, `long`, `double`, `string`, `DateTime`, `DateTimeOffset`

### Factory methods

```csharp
// String comparison
EventDataFilter.Create("customer.tier", FilterOperator.Equals, "gold");
EventDataFilter.Create("customer.name", FilterOperator.StartsWith, "Acme");
EventDataFilter.Create("order.status", FilterOperator.Contains, "pending");

// Numeric comparison
EventDataFilter.Create("payment.amount", FilterOperator.GreaterThan, 100.0);
EventDataFilter.Create("payment.amount", FilterOperator.LessThanOrEqual, 500.0);

// Boolean comparison
EventDataFilter.Create("payment.isPaid", FilterOperator.Equals, true);

// Date/time comparison
EventDataFilter.Create("order.createdAt", FilterOperator.GreaterThan,
    new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));

// Existence checks (no reference value required)
EventDataFilter.Exists("customer.loyaltyCard");
EventDataFilter.NotExists("order.deletedAt");
```

### `FilterOperator` values

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
| `Exists` | any | The property exists regardless of its value |
| `NotExists` | any | The property is absent from the payload |

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

## Registering Subscriptions at Configuration Time

All `Subscribe` overloads on `EventPublisherBuilder` accept a filter:

```csharp
// 1. Type pattern shortcut (trailing * = prefix match)
builder.Subscribe("com.example.order.*", HandleOrder);

// 2. Pre-built filter
var filter = EventAttributeFilter.Type("com.example.invoice.issued");
builder.Subscribe(filter, HandleInvoice);

// 3. Fluent builder inline
builder.Subscribe(
    fb => fb
        .WithTypePattern("com.example.*")
        .WithField("customer.tier", FilterOperator.Equals, "gold"),
    HandleGoldTierEvent,
    name: "gold-tier-handler");

// 4. Combining attribute and data filters
builder.Subscribe(
    LogicalEventFilter.And(
        EventAttributeFilter.Type("com.example.order.placed"),
        EventDataFilter.Create("payment.amount", FilterOperator.GreaterThan, 500.0)),
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
        var filter = LogicalEventFilter.And(
            EventAttributeFilter.Type("com.example.order.*", parseWildcard: true),
            EventAttributeFilter.ForExtension("tenantid", tenantId));

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

Every `IEventFilter` can be tested outside of the dispatcher pipeline:

```csharp
var filter = LogicalEventFilter.And(
    EventAttributeFilter.Type("com.example.order.placed"),
    EventDataFilter.Create("payment.isPaid", FilterOperator.Equals, true));

bool matches = filter.Matches(cloudEvent, EventSubscriptionContext.Empty);
```
