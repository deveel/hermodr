# Publish Pipeline & Middleware

Every call to `PublishEventAsync` on an `EventPublisher` passes through a structured,
ordered pipeline.  The pipeline is composed at **DI-registration time** from an immutable
descriptor and compiled exactly once when the publisher singleton is first resolved.

---

## Pipeline overview

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
  │       Use<TMiddleware>() registrations run in order (frozen at build time)
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

> **Important:** validation happens in the terminal step **after** middleware runs.
> This means middleware can enrich the event further, replace the event, or update
> per-call options before the publisher validates the envelope and fans out to channels.

---

## Stage 1 — Enrichment

The four enrichment methods run in sequence before any validation occurs, so values they
fill in satisfy the subsequent validation check automatically.

| Method | What it fills | Condition |
|--------|---------------|-----------|
| `SetEventId(CloudEvent)` | `id` attribute | Only when `id` is `null` and an `IEventIdGenerator` is registered |
| `SetTimeStamp(CloudEvent)` | `time` attribute | Only when `time` is `null` and an `IEventSystemTime` is registered |
| `SetSource(CloudEvent)` | `source` attribute | Only when `source` is `null` and `EventPublisherOptions.Source` is set |
| `SetAttributes(CloudEvent)` | Extension attributes from `EventPublisherOptions.Attributes` | Always; existing attributes are overwritten |

All four methods return the (mutated) `CloudEvent` and are `protected virtual` — override any
of them in a subclass to customise enrichment without touching the rest of the pipeline.  See
[Extending EventPublisher](event-publisher.md#extending-eventpublisher) for examples.

---

## Stage 2 — `EventContext` creation

After enrichment, the publisher creates an `EventContext` for the current publish operation.
This context is passed through the middleware chain and carries:

| Property | Description |
|----------|-------------|
| `EventContext.Event` | The enriched `CloudEvent` (middleware may mutate or replace it) |
| `EventContext.Services` | The runtime `IServiceProvider` for the current publish call |
| `EventContext.CancellationToken` | The cancellation token for the current publish call |
| `EventContext.Options` | Any per-call `EventPublishOptions` passed by the caller |

---

## Stage 3 — Middleware

Middleware is a **registration-time** concept: steps are added to the pipeline when the
publisher is configured via `EventPublisherBuilder.Use<TMiddleware>()`.  The pipeline
descriptor is **frozen** (compiled into an immutable `EventPublisherPipelineDescriptor`)
the first time the publisher singleton is resolved from the container.  You cannot add or
remove middleware after that point.

### Core types

| Type | Role |
|------|------|
| `IEventMiddleware` | A composable step in the publish pipeline |
| `EventContext` | Carries the current `CloudEvent`, `IServiceProvider`, cancellation token, and per-call options |
| `EventPublishDelegate` | `Task InvokeAsync(EventContext)` — represents the remainder of the pipeline (`next`) |

### Registering middleware at build time

Add middleware on the `EventPublisherBuilder` during DI registration:

```csharp
builder.Services
    .AddEventPublisher()
    .Use<CorrelationIdMiddleware>()
    .Use<MetricsMiddleware>();
```

You can also pass explicit extra constructor arguments (activated via `ActivatorUtilities`):

```csharp
builder.Services
    .AddEventPublisher()
    .Use<AuditMiddleware>("orders");  // "orders" forwarded as an extra ctor arg
```

### Ordering and activation rules

- Middleware runs in **registration order** — the first `Use<T>()` call becomes the
  **outermost** wrapper (closest to the caller).
- A **fresh middleware instance** is created for every publish call via `ActivatorUtilities`.
- Constructor dependencies are resolved from `EventContext.Services` at invocation time,
  which means **scoped services are supported**.
- The pipeline is compiled **once** when the publisher is first resolved; it cannot be
  changed after that point.

### Implementing `IEventMiddleware`

A middleware class implements `IEventMiddleware.InvokeAsync(EventContext, EventPublishDelegate)`:

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

Register it at build time:

```csharp
builder.Services
    .AddEventPublisher()
    .Use<CorrelationIdMiddleware>()
    .AddChannel<RabbitMqPublishChannel>();
```

### Short-circuiting

Middleware can stop publishing by **not** calling `next(context)`.  No channel will
receive the event if short-circuited:

```csharp
public sealed class DeduplicationMiddleware : IEventMiddleware
{
    public async Task InvokeAsync(EventContext context, EventPublishDelegate next)
    {
        var store = context.Services.GetRequiredService<IEventDeduplicationStore>();
        if (await store.HasSeenAsync(context.Event.Id!, context.CancellationToken))
            return;   // stop here — do not forward to channels

        await next(context);
    }
}
```

### Common middleware patterns

| Pattern | Description |
|---------|-------------|
| Enrichment | Add correlation IDs, tenant IDs, trace attributes |
| Validation / policy | Inspect `context.Event` and block selected publishes |
| Observability | Log, trace, and measure latency before/after `next(context)` |
| Subscription dispatching | Wired automatically by `AddSubscriptions()` — see below |

### Dispatcher middleware (subscriptions)

The `Deveel.Events.Subscriptions` package wires `EventDispatcher` as a middleware step
**automatically** when you call `AddSubscriptions()` on the builder.  No manual
`UseDispatcher()` call on the publisher instance is needed (the method still exists for
source compatibility but is a **no-op**).

```csharp
builder.Services
    .AddEventPublisher()
    .AddSubscriptions(subs =>
    {
        subs.ConfigureOptions(o => o.ThrowOnHandlerError = true);
        subs.Subscribe("com.example.order.*", HandleOrderAsync);
        subs.Subscribe<AuditSubscription>();
    })
    .AddChannel<RabbitMqPublishChannel>();
```

See [Subscriptions Dispatcher](../subscriptions/dispatcher.md) for full details.

---

## Stage 4 — Validation

`ValidateCloudEvent` checks the four mandatory CloudEvents 1.0 attributes **after** all
middleware has run, in the terminal step.

| Attribute | Auto-filled? | Must be caller-supplied when… |
|-----------|-------------|-------------------------------|
| `id` | ✅ via `IEventIdGenerator` | Never (always generated if absent) |
| `source` | ✅ from `EventPublisherOptions` | Options `Source` is `null` and caller did not set it |
| `type` | ❌ | Always — no default exists |
| `specversion` | ✅ by the CloudNative SDK (always `"1.0"`) | Never in practice |

If any required attribute is still absent after middleware completes,
`ValidateCloudEvent` throws `InvalidCloudEventException` **before** dispatching to any
channel.  This exception is always propagated regardless of the `ThrowOnErrors` option,
because a structurally invalid envelope is a programming error, not a transient delivery
failure.

### `InvalidCloudEventException`

`InvalidCloudEventException` subclasses `ArgumentException` and exposes a
`MissingAttributes` property listing every failing attribute so all problems surface in a
single throw.

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

---

## Stage 5 — Dispatch

The publisher's dispatch stage:

1. **Name filtering** — `FilterChannelsByName` narrows the channel list when the
   caller-supplied `options` implement `INamedChannelFilter` with a non-empty `ChannelName`.
   Only channels that implement `INamedEventPublishChannel` with a matching `Name` are kept;
   anonymous channels always pass through.  `CombinedPublishOptions` does not implement
   `INamedChannelFilter`; for combined options, name matching is done per bundled entry inside
   `ResolveChannelOptions`.
2. **Per-channel options resolution** — `ResolveChannelOptions` extracts the compatible
   `EventPublishOptions` entry for the current channel (see
   [Per-call publish options](event-publisher.md#per-call-publish-options)).
3. **Fan-out** — `PublishEventAsync(IEventPublishChannel, CloudEvent, EventPublishOptions?)`
   forwards the call to `channel.PublishAsync`.

If a channel throws and `ThrowOnErrors` is `false` (the default), the error is logged and
delivery continues to the remaining channels.  When `ThrowOnErrors` is `true` the exception
is wrapped in `EventPublishException` and re-thrown, stopping fan-out immediately.

---

## Middleware vs. subclassing

Middleware and subclassing are complementary extensibility mechanisms:

| Need | Prefer |
|------|--------|
| Add cross-cutting logic before/after publish | `Use<TMiddleware>()` on the builder |
| Short-circuit selected publishes | `Use<TMiddleware>()` on the builder |
| Resolve scoped services during publish | `Use<TMiddleware>()` on the builder |
| Change enrichment defaults globally | Subclass + override `Set*` methods |
| Change validation semantics | Subclass + override `ValidateCloudEvent` |
| Change channel selection / options matching | Subclass + override dispatch-related protected methods |

See [Extending EventPublisher](event-publisher.md#extending-eventpublisher) for full
subclassing examples.

---

## Related pages

- [Event Publisher](event-publisher.md)
- [Event Creation](event-creation.md)
- [Publish Channels](publish-channels.md)
- [Named Channels](../publishers/named-channels.md)
- [Subscriptions Dispatcher](../subscriptions/dispatcher.md)

