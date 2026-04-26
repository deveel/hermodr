# Event Publisher

## Design overview

### `IEventPublisher` — the injection contract

`IEventPublisher` is the **DI injection contract** that application code depends on.  
It exposes three publish methods, covering the full spectrum of call sites:

```csharp
public interface IEventPublisher
{
    // Publish a pre-built CloudEvent directly.
    Task PublishEventAsync(
        CloudEvent @event,
        EventPublishOptions? options = null,
        CancellationToken cancellationToken = default);

    // Build a CloudEvent from an annotated data object and publish it.
    // The eventType must carry [Event] annotations or implement IEventConvertible.
    Task PublishAsync(
        Type eventType,
        object? data,
        EventPublishOptions? options = null,
        CancellationToken cancellationToken = default);
}
```

`PublishAsync` is the **primary entry points** for most application code. It accepts annotated 
domain objects, delegate `CloudEvent` construction to 
`IEventCreator`, and then push the result through the same enrich → validate → dispatch 
pipeline as `PublishEventAsync`.  Because these methods belong to the interface, injecting 
`IEventPublisher` gives callers access to annotation-driven publishing without any coupling to 
the concrete `EventPublisher` class.

An extension method `PublishAsync<TEvent>(TEvent @event, ...)` 
is also available for an even more concise syntax when the event type is known at compile time.

> **Warning — implementing `IEventPublisher` directly**
>
> Implementing the interface from scratch means you own the **entire** publish pipeline:  
> enrichment, validation, channel fan-out, per-call options resolution, typed-channel dispatch,
> error handling and logging.  Only do this when you need to **completely replace** the pipeline
> (e.g. a test double that captures events in memory, or an exotic transport that cannot be 
> modelled as a channel).  For all other customisation needs, **extend `EventPublisher`** instead
> (see [Extending `EventPublisher`](#extending-eventpublisher) below).

### `EventPublisher` — the default implementation

`EventPublisher` is the production implementation registered by `AddEventPublisher()`.  
It provides:

- A structured, overridable **publish pipeline** (enrich → validate → dispatch).
- **Fan-out** delivery to every registered `IEventPublishChannel`.
- Automatic routing to **typed channels** (`IEventPublishChannel<TEvent>`) when available.
- **Per-call options** resolution and forwarding per channel.
- Annotation-driven `CloudEvent` creation via `IEventCreator` / `PublishAsync`.
- Structured logging and configurable error-propagation policy.

Because every stage is exposed as a `protected virtual` method, you can override exactly the 
behaviour you need without reimplementing the rest.

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
Each stage is a `protected virtual` method that subclasses can override individually.

```
PublishEventAsync(CloudEvent, options)
  │
  ├─ 1. Enrich ──────────────────────────────────────────────────────────────
  │       SetEventId(event)        → fills id  via IEventIdGenerator
  │       SetTimeStamp(event)      → fills time via IEventSystemTime
  │       SetSource(event)         → fills source from EventPublisherOptions
  │       SetAttributes(event)     → merges EventPublisherOptions.Attributes
  │
  ├─ 2. Validate ────────────────────────────────────────────────────────────
  │       ValidateCloudEvent(event)
  │         → checks id, source, type, specversion
  │         → throws InvalidCloudEventException if any are missing
  │
  └─ 3. Dispatch (fan-out) ──────────────────────────────────────────────────
          for each IEventPublishChannel:
            ResolveChannelOptions(channel, options) → per-channel options
            PublishEventAsync(channel, event, resolvedOptions)
              → channel.PublishAsync(event, resolvedOptions)
```

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

### Stage 2 — Validation

`ValidateCloudEvent` checks the four mandatory CloudEvents 1.0 attributes after enrichment.

| Attribute | Auto-filled? | Must be caller-supplied when… |
|-----------|-------------|-------------------------------|
| `id` | ✅ via `IEventIdGenerator` | Never (always generated if absent) |
| `source` | ✅ from `EventPublisherOptions` | Options source is `null` and caller did not set it |
| `type` | ❌ | Always — no default exists |
| `specversion` | ✅ by the CloudNative SDK (always `"1.0"`) | Never in practice |

If any required attribute remains absent after enrichment, `ValidateCloudEvent` throws 
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

### Stage 3 — Dispatch

The publisher iterates over every registered `IEventPublishChannel` in order:

1. `ResolveChannelOptions` is called to extract the compatible `EventPublishOptions` entry 
   for the current channel from the caller-supplied `options` argument 
   (see [Per-call publish options](#per-call-publish-options)).
2. `PublishEventAsync(IEventPublishChannel, CloudEvent, EventPublishOptions?)` forwards the 
   call to `channel.PublishAsync`.

If a channel throws and `ThrowOnErrors` is `false` (the default), the error is logged and 
delivery continues to the remaining channels.  When `ThrowOnErrors` is `true` the exception 
is wrapped in `EventPublishException` and re-thrown, stopping fan-out.

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
        // PublishAsync<TEvent> is an extension to IEventPublisher — no cast needed.
        await _publisher.PublishAsync(orderPlaced);
    }
}
```

Application code should always depend on **`IEventPublisher`**, not on the concrete 
`EventPublisher` class.  This keeps services decoupled from the publishing infrastructure and 
makes testing straightforward.

### From an annotated data class

 The method `PublishAsync(Type, object?, …)` is defined directly on 
`IEventPublisher`, and the extension method `PublishAsync<TEvent>` is available, 
 so **every caller that holds the interface** can use annotation-driven 
publishing without any cast or downcast to the concrete `EventPublisher`.

```csharp
[Event("order.placed")]
public class OrderPlaced { /* … */ }

// Works with IEventPublisher — no EventPublisher-specific API needed.
await publisher.PublishAsync(new OrderPlaced { /* … */ });
```

`PublishAsync<TEvent>` delegates to `CreateEventFromData` internally, which calls `IEventCreator` 
to build the `CloudEvent` from the annotations on `TEvent`.  The event then flows through the 
normal enrich → validate → dispatch pipeline.

If the data type is only known at runtime (e.g. from a reflection-based dispatch layer), use 
the non-generic overload — also part of `IEventPublisher`:

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
// Only RabbitMqEventPublishChannel<OrderPlaced> picks this up.
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

### Protected virtual extension points

| Method | Stage | Common override reason |
|--------|-------|------------------------|
| `SetEventId(CloudEvent)` | Enrichment | Use a custom ID format or scheme |
| `SetTimeStamp(CloudEvent)` | Enrichment | Apply a logical / frozen clock |
| `SetSource(CloudEvent)` | Enrichment | Derive `source` from request context |
| `SetAttributes(CloudEvent)` | Enrichment | Inject tenant ID, correlation ID, or other context attributes |
| `ValidateCloudEvent(CloudEvent)` | Validation | Add domain-specific attribute requirements |
| `CreateEventFromData(Type, object?)` | Event creation | Customise how annotated data classes become `CloudEvent`s |
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

This replaces the `IEventPublisher` registration with your type while keeping all other 
services (channels, ID generator, system time, etc.) intact.

### Example — tightening validation

Override only `ValidateCloudEvent` to require extra attributes:

```csharp
public class StrictPublisher : EventPublisher
{
    public StrictPublisher(
        IOptions<EventPublisherOptions> options,
        IEnumerable<IEventPublishChannel> channels,
        IEventCreator? eventCreator = null,
        IEventIdGenerator? idGenerator = null,
        IEventSystemTime? systemTime = null,
        ILogger<StrictPublisher>? logger = null)
        : base(options, channels, eventCreator, idGenerator, systemTime, logger) { }

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

## When to implement `IEventPublisher` directly

Implement the interface from scratch **only** when you need to replace the entire publish 
pipeline.  Typical cases:

| Scenario | Recommended approach |
|---|---|
| In-memory test double (captures events for assertion) | Implement `IEventPublisher` directly |
| Null publisher (no-op, e.g. integration-test isolation) | Implement `IEventPublisher` directly |
| Completely different fan-out strategy (e.g. priority queues, event sourcing) | Implement `IEventPublisher` directly |
| Add custom enrichment / context stamping | **Extend `EventPublisher`**, override `SetAttributes` |
| Stricter or relaxed validation | **Extend `EventPublisher`**, override `ValidateCloudEvent` |
| Custom options matching per channel | **Extend `EventPublisher`**, override `ResolveChannelOptions` |
| Retry / circuit-breaking around channel calls | **Extend `EventPublisher`**, override `PublishEventAsync(channel, …)` |

For a test double you can use the `TestPublisher` provided by the 
`Deveel.Events.TestPublisher` package, which already implements `IEventPublisher` and records
published events for later assertion.

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
- [Event Annotations](event-annotations.md)

