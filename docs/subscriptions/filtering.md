# Subscription Filters

`EventSubscriptionFilter` defines the criteria used to match an incoming `CloudEvent` against a subscription. **All non-null constraints must pass** for the filter to match — they are implicitly ANDed together.

## Filter Properties

| Property | CloudEvent Attribute | Description |
|----------|---------------------|-------------|
| `TypeFilter` | `type` | Match the event type string |
| `SourceFilter` | `source` (as URI string) | Match the event source |
| `SubjectFilter` | `subject` | Match the event subject |
| `ExtensionFilters` | Any extension attribute | Match one or more extension attribute values |
| `DataFilter` | event body / data | Inspect the event payload |
| `Predicate` | *(whole CloudEvent)* | An arbitrary `Func<CloudEvent, bool>` evaluated last |

Evaluation order: type → source → subject → extensions → data → predicate.  
The first constraint that **fails** short-circuits the filter — remaining ones are not evaluated.

---

## Attribute Filters (`EventAttributeFilter`)

Each attribute property holds an `EventAttributeFilter` — a value plus a match mode:

| Factory method | `FilterMatchMode` | Example pattern | Matches                                                      |
|----------------|------------------|-----------------|--------------------------------------------------------------|
| `EventAttributeFilter.Exact(value)` | `Exact` | `"com.example.order.placed"` | Exact string (ordinal, case-sensitive)                       |
| `EventAttributeFilter.Prefix(prefix)` | `Prefix` | `"com.example."` | Starts with the prefix                                       |
| `EventAttributeFilter.Suffix(suffix)` | `Suffix` | `".placed"` | Ends with the suffix                                         |
| `EventAttributeFilter.Parse(pattern)` | Auto-detected | `"com.example.*"` | Trailing `*` → Prefix; leading `*` → Suffix; otherwise Exact |

---

## Building Filters

### Static Factory Helpers

`EventSubscriptionFilter` exposes two quick factory methods for the most common case — matching by event type:

```csharp
// Exact type match
var filter = EventSubscriptionFilter.ForType("com.example.order.placed");

// Pattern match (trailing * = prefix, leading * = suffix)
var filter = EventSubscriptionFilter.ForTypePattern("com.example.order.*");
```

### Fluent Builder

For more complex requirements use `EventSubscriptionFilter.Builder`:

```csharp
var filter = EventSubscriptionFilter.Builder
    .WithType("com.example.order.placed")          // exact type
    .WithSourcePattern("https://orders.*")          // source prefix match
    .WithSubjectPattern("*.vip")                    // subject suffix match
    .WithExtension("tenantid", "acme")              // extension attribute exact match
    .Build();
```

All fluent methods accepting a `pattern` argument follow the same wildcard rules as `EventAttributeFilter.Parse`.

---

## Data / Body Filters

Body filters are evaluated **after** all attribute filters pass, which avoids unnecessary deserialization.

### JSON Path Filter

Navigate a dot-separated path inside the JSON body and compare the leaf value:

```csharp
// Exact value
var filter = EventSubscriptionFilter.Builder
    .WithType("com.example.order.placed")
    .WithJsonPath("order.customer.tier", "gold")
    .Build();

// Pattern value
var filter = EventSubscriptionFilter.Builder
    .WithJsonPathPattern("order.source", "mobile.*")
    .Build();

// Custom attribute filter
var filter = EventSubscriptionFilter.Builder
    .WithJsonPath("order.status", EventAttributeFilter.Suffix("pending"))
    .Build();
```

### JSON Predicate Filter

Evaluate an arbitrary predicate against the raw `JsonElement` root of the body:

```csharp
var filter = EventSubscriptionFilter.Builder
    .WithJsonPredicate(root =>
        root.TryGetProperty("amount", out var amount) &&
        amount.GetDecimal() > 100m)
    .Build();
```

### Typed Data Filter

Deserialize the body to a CLR type and evaluate a predicate. Deserialization is driven by the event's `datacontenttype` (JSON by default):

```csharp
var filter = EventSubscriptionFilter.Builder
    .WithData<OrderPlacedData>(order => order.TotalAmount > 500)
    .Build();
```

Supply custom `JsonSerializerOptions` if needed:

```csharp
var filter = EventSubscriptionFilter.Builder
    .WithData<OrderPlacedData>(
        order => order.TotalAmount > 500,
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
    .Build();
```

For multi-format payloads (JSON + Protobuf, etc.) supply a configured `EventDataDeserializerProvider`:

```csharp
var provider = new EventDataDeserializerProvider();
provider.Register(new JsonEventDataDeserializer());
provider.Register(new ProtobufEventDataDeserializer());

var filter = EventSubscriptionFilter.Builder
    .WithData<OrderPlacedData>(order => order.TotalAmount > 500, provider)
    .Build();
```

### Serializable Filter Expressions (Advanced)

When filter logic must survive application restarts (e.g. stored in a database), use `FilterExpression` trees instead of code-based predicates. See [Filter Expressions](filter-expressions.md) for details.

```csharp
var filter = EventSubscriptionFilter.Builder
    .WithDataExpression(
        FilterExpression.And(
            FilterExpression.JsonPath("customer.tier", "gold"),
            FilterExpression.JsonPath("amount", FilterOperator.GreaterThan, "100")))
    .Build();
```

---

## Custom CloudEvent Predicate

For logic that depends on CloudEvent attributes that are not covered by the built-in properties, attach a `Predicate`:

```csharp
var filter = EventSubscriptionFilter.Builder
    .WithTypePattern("com.example.*")
    .WithPredicate(e => e.Time?.Year == DateTimeOffset.UtcNow.Year)
    .Build();
```

The predicate receives the full `CloudEvent` and is evaluated **after** all other constraints pass.

---

## Registering Subscriptions at Configuration Time

All three `Subscribe` overloads on `EventPublisherBuilder` accept a filter:

```csharp
// 1. Type pattern shortcut
builder.Subscribe("com.example.order.*", HandleOrder);

// 2. Pre-built filter
var filter = EventSubscriptionFilter.ForType("com.example.invoice.issued");
builder.Subscribe(filter, HandleInvoice);

// 3. Fluent builder inline
builder.Subscribe(
    fb => fb.WithTypePattern("com.example.*").WithExtension("tenantid", "acme"),
    HandleTenantEvent,
    name: "acme-tenant-handler");
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
        var filter = EventSubscriptionFilter.Builder
            .WithTypePattern("com.example.order.*")
            .WithExtension("tenantid", tenantId)
            .Build();

        var subscription = new EventSubscription(
            filter,
            (e, token) => ProcessOrderAsync(e, token),
            name: $"order-handler-{tenantId}");

        await _registry.RegisterAsync(subscription, ct);
    }
}
```

