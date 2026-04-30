# Event Publisher

## Design overview

### `IEventPublisher` — the contract to depend on

**`IEventPublisher` is the interface your application code should inject and depend on.**
It is the stable, minimal contract for publishing events and is the only type you should
reference in services, controllers, handlers, or any other consumer of the publisher.

```csharp
// ✅ Correct — depend on the interface
public class OrderService(IEventPublisher publisher) { … }

// ⚠️  Avoid in application code — couples to the implementation
public class OrderService(EventPublisher publisher) { … }
```

`IEventPublisher` exposes two method families:

| Method | When to use |
|--------|-------------|
| `PublishEventAsync(CloudEvent, options?, ct)` | You already have a fully-constructed `CloudEvent`. |
| `PublishAsync(Type, object?, options?, ct)` | You have an annotated data object and want the framework to build the `CloudEvent` for you. |

The generic convenience overload `PublishAsync<TEvent>(TEvent, options?, ct)` (on `EventPublisherExtensions`) 
wraps the non-generic version for compile-time type safety.

### `EventPublisher` — the default implementation

`EventPublisher` is the **production implementation** registered by `AddEventPublisher()`.
It is an infrastructure type that provides all the boilerplate your event-publishing needs:

- A structured, fully overridable **publish pipeline**: filter → middleware → enrich → validate → dispatch.
- **Fan-out** delivery to every registered `IEventPublishChannel`.
- Automatic routing to **typed channels** (`IEventPublishChannel<TEvent>`) when available.
- **Per-call options** resolution and forwarding per channel.
- Annotation-driven `CloudEvent` creation via `IEventFactory`.
- A composable **middleware pipeline** via `EventPublisherBuilder.Use<TMiddleware>()` and `UseWhen<TMiddleware>(predicate)`.
- Support for **multiple named publisher pipelines**, each isolated with its own channels and middleware.
- Structured logging and configurable error-propagation policy (`ThrowOnErrors`).

`EventPublisher` is registered as a singleton and is exposed as both the non-keyed `IEventPublisher`
and as the concrete `EventPublisher` type (the latter only for backward compatibility — prefer `IEventPublisher`).

> **Rule of thumb:** your application code should never import or reference `EventPublisher`
> directly.  Treat it as a framework-internal implementation detail that happens to be a
> `public` class for extensibility purposes.

### `EventPublisherPipeline` — the configured middleware chain

`EventPublisherPipeline` holds the **ordered list of middleware registrations** built for a
publisher during DI setup.  It is passed to the `EventPublisher` constructor by the
`EventPublisherBuilder` and is compiled into an executable delegate exactly once, the first
time the publisher is resolved from the container.

You can access the pipeline from the DI container for **diagnostic or testing purposes**:

```csharp
// E.g. in a health-check, startup self-test, or test fixture:
var pipeline = serviceProvider.GetRequiredService<EventPublisherPipeline>();

foreach (var reg in pipeline.MiddlewareRegistrations)
{
    Console.WriteLine(
        $"{reg.MiddlewareType.Name}  " +
        $"(conditional: {reg.IsConditional})");
}
```

`EventPublisherPipeline` is **not** part of the publishing API — call it only for inspection.
To execute the pipeline, use `IEventPublisher.PublishEventAsync` as normal.

---

## Registration

### Default (unnamed) publisher

Register the publisher in your DI container using the `AddEventPublisher` extension method.
`AddEventPublisher` returns an `EventPublisherBuilder` that you **chain fluently** to add channels,
middleware, and other services — all at registration time.

```csharp
using Deveel.Events;

// Minimal registration
builder.Services.AddEventPublisher();

// With inline options
builder.Services.AddEventPublisher(options =>
{
    options.Source = new Uri("https://myapp.example.com");
    options.ThrowOnErrors = true;
});

// Bind options from appsettings.json
builder.Services.AddEventPublisher("Events:Publisher");
```

Adding channels and middleware is done **on the builder**, not on the resolved publisher instance:

```csharp
builder.Services
    .AddEventPublisher(options => options.Source = new Uri("https://myapp.example.com"))
    .Use<CorrelationIdMiddleware>()          // registered at build time
    .Use<AuditMiddleware>()                  // registered at build time
    .AddChannel<RabbitMqPublishChannel>();   // registered at build time
```

> **Important:** The middleware pipeline is **frozen** after the publisher is first resolved from
> the container (it is built exactly once via a `Lazy<T>`).  You cannot modify the pipeline at
> runtime after resolution.

The default publisher is exposed both as the non-keyed `IEventPublisher` and as the concrete
`EventPublisher`.  **Always inject `IEventPublisher`** in application code:

```csharp
// ✅ Preferred — decoupled from the implementation
public class OrderService(IEventPublisher publisher) { … }

// ⚠️  Only acceptable when a specific EventPublisher subclass API is needed
public class OrderService(EventPublisher publisher) { … }
```

### Named publishers — separate pipelines

You can register **multiple, fully independent** publisher pipelines by providing a name. Each
named pipeline has its own set of channels, middleware, and options. Channels registered in one
pipeline do **not** appear in another.

```csharp
// Default pipeline — handles most events
builder.Services
    .AddEventPublisher(options => options.Source = new Uri("https://myapp.example.com"))
    .Use<AuditMiddleware>()
    .AddChannel<RabbitMqPublishChannel>();

// Named pipeline — "notifications" — a separate, isolated pipeline
builder.Services.AddEventPublisher("notifications", b =>
{
    b.Configure(options =>
    {
        options.Source = new Uri("https://notifications.myapp.example.com");
        options.ThrowOnErrors = true;
    });
    b.Use<NotificationEnrichmentMiddleware>();
    b.AddChannel<WebhookPublishChannel>();
});
```

Named publishers are registered as **keyed singletons** under their name and are NOT exposed as
the unkeyed `IEventPublisher`.  Resolve a named publisher through `IEventPublisherFactory`:

```csharp
public class NotificationService(IEventPublisherFactory factory)
{
    private readonly IEventPublisher _publisher = factory.GetPublisher("notifications");

    public async Task SendAsync(NotificationSent notification, CancellationToken ct)
        => await _publisher.PublishAsync(notification, cancellationToken: ct);
}
```

You can also use keyed DI injection directly:

```csharp
public class NotificationService(
    [FromKeyedServices("notifications")] IEventPublisher publisher) { /* … */ }
```

#### `IEventPublisherFactory`

| Member | Description |
|--------|-------------|
| `GetPublisher(string name = "")` | Returns the `IEventPublisher` registered under `name`. Use `""` (or omit the argument) for the default pipeline. |

---

## `EventPublisherOptions`

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Source` | `Uri?` | `null` | Default `source` attribute of every `CloudEvent`. Recommended to set globally. |
| `ThrowOnErrors` | `bool` | `false` | Re-throws exceptions from channels instead of swallowing them. |
| `JsonSerializerOptions` | `JsonSerializerOptions?` | default | Options used when serialising JSON payloads. |
| `Attributes` | `Dictionary<string, object?>` | `{}` | Extra CloudEvent attributes added to every event. |
| `DataSchemaBaseUri` | `Uri?` | `null` | Base URI prepended to the event type to form the `dataschema` when none is specified on the event. |
| `DefaultContentType` | `string?` | `"application/cloudevents+json"` | Default MIME content type used when not specified on the event. |

### Example `appsettings.json`

```json
{
  "Events": {
    "Publisher": {
      "Source": "https://myapp.example.com",
      "ThrowOnErrors": true,
      "DataSchemaBaseUri": "https://schemas.myapp.example.com/"
    }
  }
}
```

---

## The publish pipeline

Every call to `PublishEventAsync` passes through the following ordered stages:
**filter → context → middleware → [terminal: enrich → validate → dispatch]**.

The pipeline is composed at DI-registration time from an immutable descriptor and compiled
exactly once when the publisher singleton is first resolved from the container.

```
PublishEventAsync(CloudEvent, options)
  ├─ 1. Filter channels by name
  ├─ 2. Create async scope + EventContext (raw, un-enriched event)
  ├─ 3. Run middleware chain  (frozen at build time; sees the raw event)
  └─ 4. Terminal step:
          a. Enrich  (SetEventId, SetTimeStamp, SetSource, SetAttributes)
          b. Validate (ValidateCloudEvent)
          c. Dispatch → fan-out to name-filtered channels
```

> **Key change from v1.0:** middleware now runs **before** enrichment.  This means
> middleware can freely set or inspect `id`, `time`, `source`, and custom extension
> attributes before the publisher's own enrichment hooks run.

For the full pipeline reference — including stage descriptions, `IEventMiddleware` and
`UseWhen` implementation guides, `EventContext.Items`, and `InvalidCloudEventException`
handling — see **[Publish Pipeline & Middleware](publish-pipeline.md)**.

---

## Publishing events

### Inject and use `IEventPublisher`

```csharp
public class OrderService
{
    private readonly IEventPublisher _publisher;

    public OrderService(IEventPublisher publisher) => _publisher = publisher;

    public async Task PlaceOrderAsync(OrderPlaced orderPlaced)
    {
        await _publisher.PublishAsync(orderPlaced);
    }
}
```

Prefer injecting `IEventPublisher` over the concrete `EventPublisher` to keep application code
decoupled from the implementation. The concrete `EventPublisher` type is also registered as a
convenience alias for the default pipeline when backward compatibility is needed.

### How events are created

`EventPublisher` supports two creation paths:

- **From an annotated data class** — call `PublishAsync<TEvent>(data)` or `PublishAsync(type, data)`.  
  The publisher calls `IEventFactory` (or `IEventConvertible.ToCloudEvent()` if the type implements it)
  to construct the `CloudEvent` from annotations on the data class. 

- **From a raw `CloudEvent`** — call `PublishEventAsync(@event)` when you already have a
  `CloudEvent` instance.  The publisher enriches and delivers it without any factory involvement.

For the full details — including `IEventFactory`, `IEventConvertible`, overload signatures,
and the `CreateEventFromData` protected hook — see **[Event Creation](event-creation.md)**.

### To a named channel

When more than one channel of the same transport type is registered, identify the target by
name using the `channelName` convenience overloads:

```csharp
await publisher.PublishAsync(orderPlaced, channelName: "rabbit-orders");
await publisher.PublishEventAsync(@event, channelName: "rabbit-notifications");
```

Channels declare their name at registration time via the `channelName` parameter on
`AddChannel<TChannel>(channelName: "rabbit-orders")` on the `EventPublisherBuilder`.  
Channels that have no name always receive every event regardless of any filter.

See [Named Channels](../publishers/named-channels.md) for the full guide.

### Using a named publisher pipeline

When you have registered multiple named pipelines, resolve the correct one from
`IEventPublisherFactory` and publish through it:

```csharp
public class NotificationService(IEventPublisherFactory factory)
{
    public Task SendAsync(NotificationSent @event, CancellationToken ct)
    {
        var publisher = factory.GetPublisher("notifications");
        return publisher.PublishAsync(@event, cancellationToken: ct);
    }
}
```

---

## Per-call publish options

Every `PublishAsync` and `PublishEventAsync` overload accepts an optional `EventPublishOptions?` 
parameter.  Passing a value lets you override channel-level defaults **for a single call** 
without changing the registered configuration.

### Single-channel override

Pass an instance of the concrete options type that matches the target channel.

```csharp
// Only the general RabbitMQ channel picks this up;
// every typed RabbitMQ channel and every other channel uses its defaults.
await publisher.PublishEventAsync(@event, new RabbitMqPublishOptions
{
    RoutingKey = "orders.priority",
    ExchangeName = "priority-exchange",
});
```

To override a **typed** channel, pass the corresponding typed options class:

```csharp
// Only RabbitMqPublishChannel<OrderPlaced> picks this up.
// The general RabbitMQ channel and all other channels use their defaults.
await publisher.PublishEventAsync(@event, new RabbitMqPublishOptions<OrderPlaced>
{
    RoutingKey = "orders.priority",
});
```

### Multi-channel override with `CombinedPublishOptions`

When more than one channel must receive different per-call overrides in the same call, wrap 
all the options in a `CombinedPublishOptions`:

```csharp
var overrides = new CombinedPublishOptions(
    // → general RabbitMQ channel
    new RabbitMqPublishOptions     { RoutingKey   = "orders.priority" },
    // → RabbitMQ channel typed for OrderPlaced
    new RabbitMqPublishOptions<OrderPlaced> { ExchangeName = "priority-orders" },
    // → general Webhook channel
    new WebhookPublishOptions      { EndpointUrl  = "https://partner.example.com/priority-hook" },
    // → Service Bus channel typed for OrderPlaced
    new ServiceBusPublishOptions<OrderPlaced> { QueueName = "priority-queue" });

await publisher.PublishEventAsync(@event, overrides);
```

The publisher inspects every registered channel in turn and extracts the **first compatible 
entry** from the bundle — matching general options to general channels and typed options to 
their corresponding typed channels.  Channels that have no compatible entry fall back silently 
to their registered defaults.

### Options resolution rules

The following table summarises how the publisher resolves the `options` parameter before 
forwarding it to each channel:

| `options` value passed | General channel receives | Typed channel `IEventPublishChannel<TEvent>` receives |
|---|---|---|
| `null` | `null` → uses its registered defaults | `null` → uses its registered defaults |
| A non-generic `XxxPublishOptions` instance | The override (if assignable to the channel's `TOptions`) | `null` → uses its defaults — general options are **not** forwarded to typed channels |
| A generic `XxxPublishOptions<TEvent>` instance | `null` → typed options are **not** forwarded to general channels | The override if `TEvent` matches the channel; `null` otherwise |
| `CombinedPublishOptions` | First non-typed bundled entry whose type is assignable to the channel's `TOptions`, or `null` | First bundled entry parameterised with this channel's `TEvent`, or `null` |
| Any options with `ChannelName` set (implements `INamedChannelFilter`) | The override, **only if** the channel's `INamedEventPublishChannel.Name` matches (or the channel is anonymous) | As above, with the same name-match requirement |
| `NamedChannelPublishOptions("my-channel")` | `null` — no type-specific override; only the name filter applies | `null` — only the name filter applies |

The discriminator between "general" and "typed" options is the **runtime generic type 
structure**: an options instance is considered *typed* when its actual type (or any type in 
its inheritance chain) is a closed generic type whose type arguments include the target event 
type.  Built-in typed options classes (`RabbitMqPublishOptions<TEvent>`, 
`ServiceBusPublishOptions<TEvent>`, etc.) satisfy this automatically.

Options resolution is handled by the `protected virtual ResolveChannelOptions(IEventPublishChannel, EventPublishOptions?)` 
method on `EventPublisher`.  Subclasses can override it to implement custom matching logic.

### `CombinedPublishOptions` API

| Member | Description |
|---|---|
| `CombinedPublishOptions(params EventPublishOptions[])` | Creates the bundle from a params array. Order is preserved; first match wins. |
| `CombinedPublishOptions(IEnumerable<EventPublishOptions>)` | Creates the bundle from any sequence. |
| `Options` | Read-only list of all bundled entries. |
| `GetOptions<TOptions>()` | Returns the first entry assignable to `TOptions`, or `null`. |
| `GetOptions(Type)` | Non-generic equivalent — useful when the options type is only known at runtime. |

---

## Extending `EventPublisher`

> **Extending `EventPublisher` is a rare operation.**
>
> The vast majority of cross-cutting scenarios — correlation IDs, tracing, deduplication,
> observability, conditional routing — are handled most cleanly through the **middleware
> pipeline** (`Use<TMiddleware>()` / `UseWhen<TMiddleware>(predicate)` on the builder).
> Middleware is easier to test, easier to reuse, and does not require managing constructor
> parameters.
>
> Only resort to subclassing when you need to change one of the publisher's **built-in
> decision points** that cannot be reached from middleware.  Even then, the subclass should
> override only the specific `protected virtual` method it needs and delegate everything
> else to `base`.

The class is designed for inheritance: every step of the publish pipeline is exposed as a
`protected virtual` method and the constructor receives its dependencies via standard DI,
so subclasses can call `base.XxxAsync(…)` to preserve default behaviour.

### Middleware vs subclassing

| Need | Correct approach |
|------|-----------------|
| Add correlation IDs, tenant IDs, trace attributes | **Middleware** — `Use<TMiddleware>()` |
| Skip logic based on a runtime condition | **Middleware** — `UseWhen<TMiddleware>(predicate)` |
| Short-circuit publishing for selected events | **Middleware** — `Use<TMiddleware>()` |
| Resolve scoped services during publish | **Middleware** — constructor-injected via `EventContext.Services` |
| Share data between pipeline steps | **Middleware** — `EventContext.Items` |
| Custom enrichment / context stamping | **Middleware first**; subclass + override `Set*` only if the enrichment must apply even when middleware is absent |
| Stricter or domain-specific validation | **Subclass** + override `ValidateCloudEvent` |
| Custom channel selection / name-based routing | **Subclass** + override `FilterChannelsByName` |
| Custom per-channel options matching | **Subclass** + override `ResolveChannelOptions` |
| Retry / circuit-breaking around channel calls | **Subclass** + override `PublishEventAsync(channel, …)` |

### Protected virtual extension points

| Method | Stage | Common override reason |
|--------|-------|------------------------|
| `SetEventId(CloudEvent)` | Enrichment | Use a custom ID format or scheme |
| `SetTimeStamp(CloudEvent)` | Enrichment | Apply a logical / frozen clock |
| `SetSource(CloudEvent)` | Enrichment | Derive `source` from request context |
| `SetAttributes(CloudEvent)` | Enrichment | Inject tenant ID, correlation ID, or other context attributes |
| `ValidateCloudEvent(CloudEvent)` | Validation | Add domain-specific attribute requirements |
| `CreateEventFromData(Type, object?)` | Event creation | Customise how annotated data classes become `CloudEvent`s |
| `FilterChannelsByName(IEnumerable<IEventPublishChannel>, EventPublishOptions?)` | Dispatch | Customise how named-channel filtering selects the target channel set |
| `ResolveChannelOptions(IEventPublishChannel, EventPublishOptions?)` | Dispatch | Implement custom per-channel options matching logic |
| `PublishEventAsync(IEventPublishChannel, CloudEvent, EventPublishOptions?, CancellationToken)` | Dispatch | Wrap individual channel calls (circuit-breaking, retries, metrics) |
| `PublishEventAsync(CloudEvent, EventPublishOptions?, CancellationToken)` | Entry point | Intercept or pre-process every publish call before fan-out |

### Registration

Register your subclass with `UsePublisher<T>()` on the builder:

```csharp
builder.Services
    .AddEventPublisher()
    .UsePublisher<MyCustomPublisher>();
```

This replaces the default `EventPublisher` registration with your type while keeping all other
services (channels, middleware, ID generator, system time, etc.) intact.

### Example — tightening validation

Override only `ValidateCloudEvent` to require extra attributes:

```csharp
public class StrictPublisher : EventPublisher
{
    public StrictPublisher(
        IOptions<EventPublisherOptions> options,
        IEnumerable<IEventPublishChannel> channels,
        IServiceProvider serviceProvider,
        EventPublisherPipeline? pipeline = null,
        ILogger<StrictPublisher>? logger = null)
        : base(options, channels, serviceProvider, pipeline, logger) { }

    protected override void ValidateCloudEvent(CloudEvent @event)
    {
        // Run the standard four-attribute check first
        base.ValidateCloudEvent(@event);

        // Then add application-specific rules
        if (@event.Subject == null)
            throw new InvalidCloudEventException(["subject"]);
    }
}
```

### Example — injecting correlation context

Override `SetAttributes` to stamp every event with a trace / correlation identifier sourced
from the ambient `Activity`:

```csharp
public class TracingPublisher : EventPublisher
{
    public TracingPublisher(/* same ctor params */) : base(/* … */) { }

    protected override CloudEvent SetAttributes(CloudEvent @event)
    {
        // Apply the base publisher-level attributes first
        @event = base.SetAttributes(@event);

        // Then stamp the W3C trace context
        var traceId = Activity.Current?.TraceId.ToString();
        if (traceId != null)
        {
            var attr = CloudEventAttribute.CreateExtension("traceid", CloudEventAttributeType.String);
            @event[attr] = traceId;
        }

        return @event;
    }
}
```

> **Prefer middleware for this pattern.** The subclass approach above is shown for completeness.
> The same result is achieved with less coupling by implementing `IEventMiddleware` and calling
> `Use<TraceIdMiddleware>()` — see [Publish Pipeline & Middleware](publish-pipeline.md).

### Example — per-channel retry wrapper

Override `PublishEventAsync(IEventPublishChannel, …)` to add resilience around individual
channel calls without changing fan-out or validation:

```csharp
public class ResilientPublisher : EventPublisher
{
    private readonly ResiliencePipeline _resilience;

    public ResilientPublisher(ResiliencePipeline resilience, /* … */) : base(/* … */)
        => _resilience = resilience;

    protected override Task PublishEventAsync(
        IEventPublishChannel channel,
        CloudEvent @event,
        EventPublishOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return _resilience.ExecuteAsync(
            ct => new ValueTask(base.PublishEventAsync(channel, @event, options, ct)),
            cancellationToken).AsTask();
    }
}
```

---

## Re-implementing `IEventPublisher` — handle with care

Implementing `IEventPublisher` from scratch — bypassing `EventPublisher` entirely — means
you **own the full publishing contract**:

- Enrichment (id, time, source, extension attributes)
- Middleware pipeline compilation and execution
- CloudEvent validation
- Channel fan-out and per-call options resolution
- Typed-channel routing
- Scoped DI scope management
- Error handling and logging

**This is almost never the right choice for production code.**  The only legitimate scenarios are:

| Scenario | Why it justifies a full re-implementation |
|----------|-------------------------------------------|
| In-memory test double | Needs to capture published events for assertion without any real transport. |
| Null / no-op publisher | Silently discards events during integration-test isolation. |
| Completely exotic fan-out strategy | Cannot be modelled as one or more `IEventPublishChannel` implementations. |

For tests, the `Deveel.Events.TestPublisher` package provides `AddTestChannel()`, which makes
an in-memory capture available without re-implementing the publisher at all.

> ⚠️  If you find yourself re-implementing `IEventPublisher` to add a cross-cutting concern
> (logging, tracing, enrichment, retries), **stop** — use [middleware](publish-pipeline.md#stage-3--middleware)
> or a [subclass](#extending-eventpublisher) instead.

---

## Extensibility — infrastructure hooks

### Custom ID generator

```csharp
builder.Services
    .AddEventPublisher()
    .UseGuid("N");   // compact format without dashes
```

### Custom system time

Useful in tests to control event timestamps:

```csharp
builder.Services
    .AddEventPublisher()
    .UseSystemTime<FrozenSystemTime>();
```

`FrozenSystemTime` is any class implementing `IEventSystemTime`:

```csharp
public class FrozenSystemTime : IEventSystemTime
{
    public DateTimeOffset UtcNow { get; } = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
}
```

---

## Related pages

- [Publish Pipeline & Middleware](publish-pipeline.md)
- [Event Creation](event-creation.md)
- [Publish Channels](publish-channels.md)
- [Named Channels](../publishers/named-channels.md)
- [Typed Channels](../publishers/typed-channels.md)
- [Subscriptions Dispatcher](../subscriptions/dispatcher.md)
- [Event Annotations](event-annotations.md)

