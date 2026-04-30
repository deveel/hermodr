# Event Publisher

## Design overview

### `EventPublisher` — the injection contract

`EventPublisher` is the publishing contract that application code depends on through DI.  
It exposes overloads that cover pre-built `CloudEvent` instances and annotation-driven publishing from CLR types.

`PublishAsync` is the **primary entry point** for most application code. It accepts annotated 
domain objects, delegates `CloudEvent` construction to 
`IEventFactory`, and then pushes the result through the same enrich → middleware → validate → dispatch 
pipeline as `PublishEventAsync`.

The generic overload `PublishAsync<TEvent>(TEvent @event, ...)` 
is also available for an even more concise syntax when the event type is known at compile time.

> **Warning — replacing `EventPublisher` entirely**
>
> Replacing the publisher implementation means you own the **entire** publish pipeline:  
> enrichment, validation, channel fan-out, per-call options resolution, typed-channel dispatch,
> error handling and logging.  Only do this when you need to **completely replace** the pipeline
> (e.g. a test double that captures events in memory, or an exotic transport that cannot be 
> modelled as a channel).  For all other customisation needs, **extend `EventPublisher`** instead
> (see [Extending `EventPublisher`](#extending-eventpublisher) below).

### `EventPublisher` — core capabilities

`EventPublisher` is the production implementation registered by `AddEventPublisher()`.  
It provides:

- A structured, overridable **publish pipeline** (enrich → middleware → validate → dispatch).
- **Fan-out** delivery to every registered `IEventPublishChannel`.
- Automatic routing to **typed channels** (`IEventPublishChannel<TEvent>`) when available.
- **Per-call options** resolution and forwarding per channel.
- Annotation-driven `CloudEvent` creation via `IEventFactory` / `PublishAsync`.
- A composable **middleware pipeline** configured at DI-registration time via `EventPublisherBuilder.Use<TMiddleware>()`.
- Support for **multiple named publisher pipelines**, each with its own channels and middleware.
- Structured logging and configurable error-propagation policy.

Because the publisher-owned stages are exposed as `protected virtual` methods and middleware is
composed per-builder-registration, you can customise exactly the behaviour you need without
reimplementing the rest.

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
`EventPublisher`, so you can inject either:

```csharp
public class OrderService(IEventPublisher publisher) { /* … */ }
// —or—
public class OrderService(EventPublisher publisher) { /* … */ }
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
**enrich → middleware → validate → dispatch**.

The pipeline is composed at DI-registration time from an immutable descriptor and compiled
exactly once when the publisher singleton is first resolved from the container.

```
PublishEventAsync(CloudEvent, options)
  ├─ 1. Enrich (SetEventId, SetTimeStamp, SetSource, SetAttributes)
  ├─ 2. Create EventContext
  ├─ 3. Run middleware chain  (frozen at build time)
  └─ 4. Terminal validate + dispatch → fan-out to channels
```

For the full pipeline reference — including stage descriptions, `IEventMiddleware` implementation
guide, and `InvalidCloudEventException` handling — see
**[Publish Pipeline & Middleware](publish-pipeline.md)**.

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

**Extending `EventPublisher` is the recommended customisation strategy** for the vast majority 
of scenarios.  The class is designed for inheritance: every step of the publish pipeline is 
exposed as a `protected virtual` method and the constructor receives its dependencies via 
standard DI, so subclasses can call `base.XxxAsync(…)` to preserve default behaviour and 
override only the specific step they need to change.

### Middleware vs subclassing

Use **middleware** when you want a reusable registration-time step that wraps publish execution without changing the publisher's core protected methods.

Use **subclassing** when you need to change one of the publisher's built-in decision points, such as how events are enriched, validated, filtered, or how per-channel options are resolved.

| Need | Prefer |
|------|--------|
| Add cross-cutting logic before/after publish | `Use<TMiddleware>()` on the builder |
| Short-circuit selected publishes | `Use<TMiddleware>()` on the builder |
| Resolve scoped services during publish | `Use<TMiddleware>()` on the builder |
| Change enrichment defaults globally | subclass + override `Set*` methods |
| Change validation semantics | subclass + override `ValidateCloudEvent` |
| Change channel selection / options matching | subclass + override dispatch-related protected methods |

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
services (channels, ID generator, system time, etc.) intact.

### Example — tightening validation

Override only `ValidateCloudEvent` to require extra attributes:

```csharp
public class StrictPublisher : EventPublisher
{
    public StrictPublisher(
        IOptions<EventPublisherOptions> options,
        IEnumerable<IEventPublishChannel> channels,
        IServiceProvider serviceProvider,
        ILogger<StrictPublisher>? logger = null)
        : base(options, channels, serviceProvider, logger) { }

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

## When to replace `EventPublisher` entirely

Replace the publisher implementation **only** when you need to replace the entire publish 
pipeline.  Typical cases:

| Scenario | Recommended approach |
|---|---|
| In-memory test double (captures events for assertion) | Build a dedicated `EventPublisher` replacement |
| Null publisher (no-op, e.g. integration-test isolation) | Build a dedicated `EventPublisher` replacement |
| Completely different fan-out strategy (e.g. priority queues, event sourcing) | Build a dedicated `EventPublisher` replacement |
| Add custom enrichment / context stamping | **Extend `EventPublisher`**, override `SetAttributes` |
| Stricter or relaxed validation | **Extend `EventPublisher`**, override `ValidateCloudEvent` |
| Custom channel selection / name-based routing | **Extend `EventPublisher`**, override `FilterChannelsByName` |
| Custom options matching per channel | **Extend `EventPublisher`**, override `ResolveChannelOptions` |
| Retry / circuit-breaking around channel calls | **Extend `EventPublisher`**, override `PublishEventAsync(channel, …)` |

For tests, you can use the channels in `Deveel.Events.TestPublisher` (for example `AddTestChannel`) 
to assert what was published without a real transport.

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

