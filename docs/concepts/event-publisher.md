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
- A composable **runtime middleware pipeline** configured with `Use<TMiddleware>()`.
- Structured logging and configurable error-propagation policy.

Because the publisher-owned stages are exposed as `protected virtual` methods and middleware is composed separately at runtime, you can customise exactly the behaviour you need without reimplementing the rest.

---

## Registration

Register the publisher in your DI container using the `AddEventPublisher` extension method:

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

`AddEventPublisher` returns an `EventPublisherBuilder` that you chain to register channels and 
other services.

The builder configures DI and channel registrations. Runtime middleware is added on the resolved
`EventPublisher` instance itself:

```csharp
var publisher = serviceProvider.GetRequiredService<EventPublisher>()
    .Use<CorrelationIdMiddleware>()
    .Use<AuditMiddleware>();
```

Middleware registration is per publisher instance and runs in the order you register it.

If you use `Deveel.Events.Subscriptions`, call `AddSubscriptions()` during registration and then enable the middleware on the runtime publisher instance with `UseDispatcher()` (or `UseDispatcher(EventDispatcherOptions)`).

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

Every call to `PublishEventAsync` passes through the following ordered stages.  
The publisher-owned stages remain `protected virtual`, while the middleware stage is composed at runtime.

```
PublishEventAsync(CloudEvent, options)
  │
  ├─ 1. Enrich ──────────────────────────────────────────────────────────────
  │       SetEventId(event)        → fills id  via IEventIdGenerator
  │       SetTimeStamp(event)      → fills time via IEventSystemTime
  │       SetSource(event)         → fills source from EventPublisherOptions
  │       SetAttributes(event)     → merges EventPublisherOptions.Attributes
  │
  ├─ 2. Create EventContext ─────────────────────────────────────────────────
  │       EventContext(event, services, cancellationToken, options)
  │
  ├─ 3. Run middleware chain ────────────────────────────────────────────────
  │       Use<TMiddleware>() registrations run in order
  │       middleware may mutate context.Event
  │       middleware may short-circuit by not calling next(context)
  │
  └─ 4. Terminal validate + dispatch ────────────────────────────────────────
          ValidateCloudEvent(context.Event)
          FilterChannelsByName(channels, context.Options) → named-channel filter
          for each IEventPublishChannel:
            ResolveChannelOptions(channel, context.Options) → per-channel options
            PublishEventAsync(channel, context.Event, resolvedOptions)
              → channel.PublishAsync(context.Event, resolvedOptions)
```

> **Important:** validation happens in the terminal step **after** middleware runs. This means middleware can enrich the event further, replace the event, or update per-call options before the publisher validates the envelope and fans out to channels.

### Stage 1 — Enrichment

The four enrichment methods run in sequence before any validation occurs, so values they fill 
in satisfy the subsequent validation check automatically.

| Method | What it fills | Condition |
|--------|---------------|-----------|
| `SetEventId(CloudEvent)` | `id` attribute | Only when `id` is `null` and an `IEventIdGenerator` is registered |
| `SetTimeStamp(CloudEvent)` | `time` attribute | Only when `time` is `null` and an `IEventSystemTime` is registered |
| `SetSource(CloudEvent)` | `source` attribute | Only when `source` is `null` and `EventPublisherOptions.Source` is set |
| `SetAttributes(CloudEvent)` | Extension attributes from `EventPublisherOptions.Attributes` | Always; existing attributes are overwritten |

All four methods return the (mutated) `CloudEvent` and are `protected virtual` — override any 
of them to customise enrichment without touching the rest of the pipeline.

### Stage 2 — `EventContext` creation

After enrichment, the publisher creates an `EventContext` for the current publish operation. This context carries:

- `EventContext.Event` — the enriched `CloudEvent`
- `EventContext.Services` — the runtime `IServiceProvider`
- `EventContext.CancellationToken` — the cancellation token for the current publish call
- `EventContext.Options` — any per-call `EventPublishOptions`

Middleware receives and can mutate this context before the terminal validate-and-dispatch step runs.

### Stage 3 — Middleware

If any middleware has been added with `Use<TMiddleware>()`, the publisher composes it around the terminal step.

- Middleware runs in registration order
- Middleware may inspect or replace `context.Event`
- Middleware may short-circuit publishing by not calling `next(context)`

### Stage 4 — Validation

`ValidateCloudEvent` checks the four mandatory CloudEvents 1.0 attributes in the terminal step, after enrichment and after all middleware has run.

| Attribute | Auto-filled? | Must be caller-supplied when… |
|-----------|-------------|-------------------------------|
| `id` | ✅ via `IEventIdGenerator` | Never (always generated if absent) |
| `source` | ✅ from `EventPublisherOptions` | Options source is `null` and caller did not set it |
| `type` | ❌ | Always — no default exists |
| `specversion` | ✅ by the CloudNative SDK (always `"1.0"`) | Never in practice |

If any required attribute remains absent after middleware completes, `ValidateCloudEvent` throws 
`InvalidCloudEventException` **before** dispatching to any channel.  This exception is always 
propagated regardless of the `ThrowOnErrors` option, because a structurally invalid envelope 
is a programming error, not a transient delivery failure.

#### `InvalidCloudEventException`

`InvalidCloudEventException` subclasses `ArgumentException` and exposes a `MissingAttributes` 
property that lists every failing attribute so all problems surface in a single throw.

```csharp
try
{
    await publisher.PublishEventAsync(incompleteEvent);
}
catch (InvalidCloudEventException ex)
{
    // ex.MissingAttributes → e.g. ["type", "source"]
    Console.WriteLine($"Invalid event: {ex.Message}");
}
```

### Stage 5 — Dispatch

The publisher's dispatch stage has two steps before iterating channels:

1. **Name filtering** — `FilterChannelsByName` narrows the channel list when the caller-supplied `options` implement `INamedChannelFilter` with a non-empty `ChannelName`.  Only channels that implement `INamedEventPublishChannel` with a matching `Name` are kept; anonymous channels (those that do not implement `INamedEventPublishChannel`, or have a `null`/empty name) always pass through.  `CombinedPublishOptions` does not implement `INamedChannelFilter`; for combined options, name matching is done per bundled entry inside `ResolveChannelOptions`.
2. For each remaining channel, `ResolveChannelOptions` is called to extract the compatible `EventPublishOptions` entry for the current channel from the caller-supplied `options` argument (see [Per-call publish options](#per-call-publish-options)).
3. `PublishEventAsync(IEventPublishChannel, CloudEvent, EventPublishOptions?)` forwards the call to `channel.PublishAsync`.

If a channel throws and `ThrowOnErrors` is `false` (the default), the error is logged and delivery continues to the remaining channels.  When `ThrowOnErrors` is `true` the exception is wrapped in `EventPublishException` and re-thrown, stopping fan-out.

---

## Middleware pipeline

Middleware is one of the runtime concepts in `EventPublisher`. It lets you attach composable processing steps around the terminal validate-and-fan-out stage without subclassing the publisher.

### Core types

| Type | Role |
|------|------|
| `IEventMiddleware` | A composable step in the publish pipeline |
| `EventContext` | Carries the current `CloudEvent`, `IServiceProvider`, cancellation token, and per-call options |
| `EventPublishDelegate` | Represents the remainder of the pipeline (`next`) |

### Runtime registration

Add middleware on the resolved publisher instance:

```csharp
var publisher = serviceProvider.GetRequiredService<EventPublisher>()
    .Use<CorrelationIdMiddleware>()
    .Use<MetricsMiddleware>();
```

You can also pass explicit constructor arguments:

```csharp
var publisher = serviceProvider.GetRequiredService<EventPublisher>()
    .Use<AuditMiddleware>("orders");
```

### Ordering and activation rules

- Middleware runs in **registration order**.
- The first middleware registered is the **outermost** wrapper.
- A **fresh middleware instance** is created for every publish call via `ActivatorUtilities`.
- Constructor dependencies are resolved from `EventContext.Services`.
- Calling `Use<TMiddleware>()` after the first publish is supported; the pipeline is rebuilt on the next call.

### Common middleware patterns

- **Enrichment** — add correlation IDs, tenant IDs, or trace attributes
- **Validation / policy** — inspect `context.Event` and block certain publishes
- **Observability** — log, trace, and measure before/after `next(context)`
- **Routing hooks** — for example `UseDispatcher()` from subscriptions

### Example custom middleware

```csharp
public sealed class CorrelationIdMiddleware : IEventMiddleware
{
    public async Task InvokeAsync(EventContext context, EventPublishDelegate next)
    {
        var accessor = context.Services.GetRequiredService<ICorrelationAccessor>();

        if (!string.IsNullOrEmpty(accessor.CorrelationId))
        {
            var attr = CloudEventAttribute.CreateExtension("correlationid", CloudEventAttributeType.String);
            context.Event[attr] = accessor.CorrelationId;
        }

        await next(context);
    }
}
```

### Short-circuiting

Middleware can stop publishing by not calling `next(context)`:

```csharp
public sealed class DeduplicationMiddleware : IEventMiddleware
{
    public async Task InvokeAsync(EventContext context, EventPublishDelegate next)
    {
        var store = context.Services.GetRequiredService<IEventDeduplicationStore>();
        if (await store.HasSeenAsync(context.Event.Id!, context.CancellationToken))
            return;

        await next(context);
    }
}
```

### Dispatcher middleware

The subscriptions package builds on this pipeline model. `UseDispatcher()` adds the subscription dispatcher as middleware so subscription handling runs as part of the same publish flow:

```csharp
var publisher = serviceProvider.GetRequiredService<EventPublisher>()
    .UseDispatcher();
```

See [Subscriptions Dispatcher](../subscriptions/dispatcher.md) for details.

---

## Publishing events

### Inject and use `EventPublisher`

```csharp
public class OrderService
{
    private readonly EventPublisher _publisher;

    public OrderService(EventPublisher publisher) => _publisher = publisher;

    public async Task PlaceOrderAsync(OrderPlaced orderPlaced)
    {
        // PublishAsync<TEvent> is available directly on EventPublisher.
        await _publisher.PublishAsync(orderPlaced);
    }
}
```

Application code can depend on **`EventPublisher`** directly via DI.

### From an annotated data class

 The method `PublishAsync(Type, object?, …)` is available on 
`EventPublisher`, and the generic overload `PublishAsync<TEvent>` is also available.

```csharp
[Event("order.placed")]
public class OrderPlaced { /* … */ }

// Works directly with EventPublisher.
await publisher.PublishAsync(new OrderPlaced { /* … */ });
```

`PublishAsync<TEvent>` delegates to `CreateEventFromData` internally, which calls `IEventFactory` 
to build the `CloudEvent` from the annotations on `TEvent`.  The event then flows through the 
normal enrich → middleware → validate → dispatch pipeline.

If the data type is only known at runtime (e.g. from a reflection-based dispatch layer), use 
the non-generic overload:

```csharp
await publisher.PublishAsync(typeof(OrderPlaced), orderEvent);
```

### From a raw `CloudEvent`

```csharp
var @event = new CloudEvent
{
    Type   = "order.placed",
    Source = new Uri("https://myapp.example.com"),
    Data   = orderPayload
};

await publisher.PublishEventAsync(@event);
```

### To a named channel

When more than one channel of the same transport type is registered, identify the target by name using the `string channelName` convenience overloads:

```csharp
// Target a specific named channel by name — no channel-type knowledge needed.
await publisher.PublishAsync(orderPlaced, channelName: "rabbit-orders");
await publisher.PublishEventAsync(@event, channelName: "rabbit-notifications");
```

Channels declare their name through the `ChannelName` property on their options (which implement `INamedChannelFilter`).  Channels that have no name always receive every event regardless of any filter.

See [Named Channels](../publishers/named-channels.md) for the full guide.

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

Use **middleware** when you want a reusable runtime step that wraps publish execution without changing the publisher's core protected methods.

Use **subclassing** when you need to change one of the publisher's built-in decision points, such as how events are enriched, validated, filtered, or how per-channel options are resolved.

| Need | Prefer |
|------|--------|
| Add cross-cutting logic before/after publish | `Use<TMiddleware>()` |
| Short-circuit selected publishes | `Use<TMiddleware>()` |
| Resolve scoped services during publish | `Use<TMiddleware>()` |
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

- [Publish Channels](publish-channels.md)
- [Subscriptions Dispatcher](../subscriptions/dispatcher.md)
- [Named Channels](../publishers/named-channels.md)
- [Typed Channels](../publishers/typed-channels.md)
- [Event Annotations](event-annotations.md)

