# Filter Expressions

Event subscription filters in `Deveel.Events.Subscriptions` are built on `FilterExpression` from the [`Deveel.Filters`](https://www.nuget.org/packages/Deveel.Filters) package. A `FilterExpression` is a structured, data-driven, serializable representation of a filter predicate — unlike a plain delegate, it can be introspected, persisted, and composed programmatically.

## Motivation

Delegate-based subscriptions are convenient but opaque — they cannot be persisted to a database, loaded from external configuration, or combined at runtime without custom glue code. `FilterExpression` solves this:

- **Composable** — `And`, `Or`, `Not` operations produce new expressions from existing ones.
- **Serializable** — the expression tree can be serialized to JSON (via `Deveel.Filters`) and stored in a database, configuration file, or remote store.
- **CloudEvent-aware** — the `EventFilter` factory class maps CloudEvents envelope attributes and JSON data paths to `FilterExpression` variables that the built-in evaluator understands.

---

## Building Expressions with `EventFilter`

### Envelope attributes

```csharp
// Exact type match
FilterExpression byType = EventFilter.ByType("com.example.order.placed");

// Wildcard type match (trailing * = prefix, leading * = suffix)
FilterExpression byPattern = EventFilter.ByTypePattern("com.example.*");

FilterExpression bySource = EventFilter.BySource("https://api.example.com");
FilterExpression bySourcePattern = EventFilter.BySourcePattern("https://api.example.*");

FilterExpression bySubject = EventFilter.BySubject("order/42");
FilterExpression byExtension = EventFilter.ByExtension("tenantid", "acme");
```

### Data payload fields

Paths are dot-separated JSON paths (e.g. `"customer.tier"`). The `data.` prefix is added automatically by the evaluator.

> **JSON path validation**: each segment may only contain letters, digits, and underscores (`_`). Hyphens and other special characters are **not** allowed. An `ArgumentException` is thrown at construction time for invalid paths.

```csharp
// Exact string match on a JSON body field
FilterExpression byField = EventFilter.ByField("customer.tier", "gold");

// Numeric comparison
FilterExpression byAmount = EventFilter.ByField(
    "payment.amount", FilterExpressionType.GreaterThan, 100.0);

// Boolean field
FilterExpression byPaid = EventFilter.ByField(
    "payment.isPaid", FilterExpressionType.Equal, true);

// Date/time comparison
FilterExpression byDate = EventFilter.ByField(
    "order.createdAt", FilterExpressionType.GreaterThan,
    new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));

// String operations (not representable with FilterExpressionType)
FilterExpression startsWith = EventFilter.FieldStartsWith("customer.name", "Acme");
FilterExpression endsWith   = EventFilter.FieldEndsWith("order.ref", "EU");
FilterExpression contains   = EventFilter.FieldContains("order.status", "pending");

// Existence
FilterExpression exists    = EventFilter.FieldExists("customer.loyaltyCard");
FilterExpression notExists = EventFilter.FieldNotExists("order.deletedAt");
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

> **String operations** (StartsWith, EndsWith, Contains) and **existence checks** use dedicated `EventFilter` factory methods rather than `FilterExpressionType`.

---

## Combining Expressions

`EventFilter.All(…)` produces a logical AND over two or more expressions; `EventFilter.Any(…)` produces a logical OR. Both require at least two arguments — an `ArgumentException` is thrown otherwise.

```csharp
// AND
FilterExpression filter = EventFilter.All(
    EventFilter.ByTypePattern("com.example.order.*"),
    EventFilter.ByField("customer.tier", "gold"));

// OR
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

For exactly two operands you may use the lower-level `FilterExpression.And` / `FilterExpression.Or`:

```csharp
FilterExpression filter = FilterExpression.And(
    EventFilter.ByType("com.example.order.placed"),
    EventFilter.ByField("customer.tier", "gold"));
```

---

## Fluent Builder — `EventFilterBuilder`

`EventFilter.New()` returns an `EventFilterBuilder` that composes the same conditions in a fluent chain. All top-level conditions are ANDed. Inner OR groups are created with `AnyOf`, explicit AND sub-groups with `AllOf`, and negations with `Not`. Calling `Build()` on an empty builder returns `FilterExpression.Empty` (matches every event).

```csharp
// Basic chain — implicit AND
FilterExpression filter = EventFilter.New()
    .ByTypePattern("com.example.order.*")
    .ByExtension("tenantid", "acme")
    .WithField("customer.tier", "gold")
    .Build();

// OR group
FilterExpression orFilter = EventFilter.New()
    .AnyOf(b => b
        .ByType("com.example.order.placed")
        .ByType("com.example.order.updated"))
    .Build();

// Nested: (tier == "gold" AND amount > 100) OR priority == "urgent"
FilterExpression nestedFilter = EventFilter.New()
    .AnyOf(b => b
        .AllOf(inner => inner
            .WithField("customer.tier", "gold")
            .WithField("payment.amount", FilterExpressionType.GreaterThan, 100.0))
        .WithField("priority", "urgent"))
    .Build();

// Negation
FilterExpression notFilter = EventFilter.New()
    .ByTypePattern("com.example.order.*")
    .Not(b => b.WithField("status", "cancelled"))
    .Build();
```

> Note: the builder uses `WithField(…)` for data-payload conditions, whereas the static `EventFilter` class uses `ByField(…)`. The semantics are identical.

---

## Using Expressions in a Subscription

Pass any `FilterExpression` to `Subscribe`:

```csharp
// Type pattern shorthand (trailing * = prefix match)
builder.Subscribe("com.example.order.*", HandleOrder);

// Pre-built expression
builder.Subscribe(
    EventFilter.All(
        EventFilter.ByType("com.example.order.placed"),
        EventFilter.ByField("customer.tier", "gold")),
    HandleGoldOrder,
    name: "gold-order-handler");

// Fluent builder
builder.Subscribe(
    EventFilter.New()
        .ByTypePattern("com.example.order.*")
        .ByExtension("tenantid", "acme")
        .Build(),
    HandleTenantOrder,
    name: "tenant-order-handler");
```

---

## Evaluator Behaviour

The built-in evaluator (`EventFilterEvaluator`) resolves filter variables at runtime against the dispatched `CloudEvent`:

- A **`null` or empty** filter is treated as **match-all** — the subscription always fires.
- **AND** short-circuits: if the left operand is `false`, the right is never evaluated.
- **OR** short-circuits: if the left operand is `true`, the right is never evaluated.
- **Data field paths** are resolved via `EventSubscriptionContext.GetJsonData()`, which deserializes the event payload to `JsonElement` and caches the result for the lifetime of the dispatch cycle.
- **Extension attributes** are accessed via the `extension.<name>` variable prefix.
- **Standard envelope attributes** resolved by name: `type`, `source`, `subject`, `id`, `time`, `datacontenttype`, `dataschema`.

---

## Evaluating Expressions Directly

The `Matches` extension method (from `FilterExpressionExtensions`) evaluates any `FilterExpression` against a `CloudEvent`:

```csharp
FilterExpression filter = EventFilter.All(
    EventFilter.ByField("customer.tier", "gold"),
    EventFilter.ByField("payment.isPaid", FilterExpressionType.Equal, true));

bool matches = filter.Matches(cloudEvent, EventSubscriptionContext.Empty);
```

---

## Serialization

`FilterExpression` can be round-tripped to and from JSON using `JsonFilterConverter` — a `System.Text.Json` converter that ships with the `Deveel.Filters` package.  
The `Deveel.Events.Subscriptions` package surfaces this through two convenience members:

| Member | Where | Purpose |
|--------|-------|---------|
| `filter.ToJson(options?)` | `FilterExpressionExtensions` | Serialize to a JSON string |
| `EventFilter.FromJson(json, options?)` | `EventFilter` | Deserialize from a JSON string |

Both accept an optional `JsonSerializerOptions` argument. When the options are provided but do **not** already contain a `JsonFilterConverter`, the converter is injected automatically into a **copy** of the options — the original object is never mutated.

---

### `ToJson(JsonSerializerOptions? options = null)`

```csharp
// 1. Default — compact JSON, no extra configuration needed
FilterExpression filter = EventFilter.All(
    EventFilter.ByTypePattern("com.example.order.*"),
    EventFilter.ByField("customer.tier", "gold"));

string json = filter.ToJson();
// e.g. {"type":"and","left":{…},"right":{…}}

// 2. Custom options — pretty-print; converter is added automatically to a copy
var opts = new JsonSerializerOptions { WriteIndented = true };
string pretty = filter.ToJson(opts);
// opts itself is unchanged; the converter was added to an internal copy only

// 3. Options that already include the converter — used as-is, no duplication
var opts2 = new JsonSerializerOptions();
opts2.Converters.Add(new JsonFilterConverter());
string json2 = filter.ToJson(opts2);
```

---

### `EventFilter.FromJson(string json, JsonSerializerOptions? options = null)`

```csharp
// 1. Simplest form
FilterExpression? filter = EventFilter.FromJson(json);

// 2. Apply custom options (e.g. case-insensitive property matching)
var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
FilterExpression? filter2 = EventFilter.FromJson(json, opts);
// converter is injected automatically into a copy of opts

// 3. Reconstruct and immediately use
bool matches = EventFilter
    .FromJson(storedJson)
    ?.Matches(cloudEvent, EventSubscriptionContext.Empty)
    ?? true;   // null filter → match-all
```

**Exceptions thrown by `FromJson`**

| Exception | Cause |
|-----------|-------|
| `ArgumentException` | `json` is `null`, empty, or whitespace-only |
| `JsonException` | `json` is not valid JSON, or does not conform to the `FilterExpression` schema |

---

### Annotating a property with `[JsonConverter]`

`[JsonConverter(typeof(JsonFilterConverter))]` can be placed on a `FilterExpression` property to control how that specific property is written.

> **Important — serialization only**: the attribute is sufficient for **writing** (serializing), but **not for reading** (deserializing). `JsonFilterConverter` internally calls back into the serializer with the same options to dispatch polymorphic sub-expression types. When the converter is registered only at the property level those recursive calls cannot resolve `FilterExpression`'s concrete sub-types, which results in a `NotSupportedException` ("Deserialization of interface or abstract types is not supported").

```csharp
using System.Text.Json.Serialization;
using Deveel.Filters;

public class SubscriptionDto
{
    public string Name { get; set; } = string.Empty;

    // ✔ writes correctly; ✘ deserialization will throw NotSupportedException
    [JsonConverter(typeof(JsonFilterConverter))]
    public FilterExpression? Filter { get; set; }
}
```

For full round-trip support on a DTO, register the converter **globally** in the `JsonSerializerOptions` used for both directions (see [Registering the converter globally](#registering-the-converter-globally)).

```csharp
public class SubscriptionDto
{
    public string Name { get; set; } = string.Empty;
    public FilterExpression? Filter { get; set; }   // no attribute needed
}

var opts = new JsonSerializerOptions();
opts.Converters.Add(new JsonFilterConverter());

var json     = JsonSerializer.Serialize(dto, opts);
var restored = JsonSerializer.Deserialize<SubscriptionDto>(json, opts)!;
// restored.Filter is a fully functional FilterExpression
```

---

### Registering the converter globally

#### `JsonSerializerOptions` (any host)

Register the converter once on your options instance and it applies to every `FilterExpression` within documents serialized with that instance:

```csharp
var options = new JsonSerializerOptions();
options.Converters.Add(new JsonFilterConverter());

// All FilterExpression properties are handled automatically
string json = JsonSerializer.Serialize(myObject, options);
```

#### ASP.NET Core

Add the converter to the MVC / Minimal-API JSON options in `Program.cs` so all request/response bodies serialize `FilterExpression` correctly:

```csharp
// Minimal API / ASP.NET Core 6+
builder.Services
    .AddControllers()
    .AddJsonOptions(o =>
        o.JsonSerializerOptions.Converters.Add(new JsonFilterConverter()));
```

Or, when using `System.Text.Json` directly through `IOptions<JsonOptions>`:

```csharp
builder.Services.Configure<JsonOptions>(o =>
    o.SerializerOptions.Converters.Add(new JsonFilterConverter()));
```

Once registered, model binding and `JsonResult` serialization will handle `FilterExpression` properties in request/response objects transparently:

```csharp
// Request body: {"name":"gold-orders","filter":{…}}
app.MapPost("/subscriptions", (SubscriptionDto dto) =>
{
    // dto.Filter is already a FilterExpression
    _registry.Register(new EventSubscription(dto.Filter, HandleOrder, dto.Name));
    return Results.Created();
});
```
