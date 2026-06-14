# Publish Pipeline & Middleware

Every call to `PublishEventAsync` on an `EventPublisher` passes through a structured,
ordered pipeline.  The pipeline is composed at **DI-registration time** from an immutable
descriptor and compiled exactly once when the publisher singleton is first resolved.

---

## Pipeline overview

```
PublishEventAsync(CloudEvent, options)
  │
  ├─ 1. Filter channels by name ─────────────────────────────────────────────
  │       FilterChannelsByName(channels, options)
  │       → narrow channel list when options.ChannelName is set
  │
  ├─ 2. Create async scope + EventContext ────────────────────────────────────
  │       await using scope = serviceProvider.CreateAsyncScope()
  │       EventContext(rawEvent, scope.ServiceProvider, cancellationToken, options)
  │       ⚠  The event has NOT been enriched yet at this point.
  │
  ├─ 3. Run middleware chain ─────────────────────────────────────────────────
  │       Use<TMiddleware>() / UseWhen<TMiddleware>(predicate) registrations
  │       run in registration order (frozen at build time)
  │       middleware may inspect or mutate context.Event (raw, un-enriched)
  │       middleware may short-circuit by not calling next(context)
  │
  └─ 4. Terminal step ────────────────────────────────────────────────────────
          a. Enrich ────────────────────────────────────────────────────────
                SetEventId(event)     → fills id  via IEventIdGenerator
                SetTimeStamp(event)   → fills time via IEventSystemTime
                SetSource(event)      → fills source from EventPublisherOptions
                SetAttributes(event)  → merges EventPublisherOptions.Attributes
          b. Validate ──────────────────────────────────────────────────────
                ValidateCloudEvent(context.Event)
                → throws InvalidCloudEventException if required attrs still absent
          c. Dispatch ──────────────────────────────────────────────────────
                for each IEventPublishChannel (already name-filtered):
                  ResolveChannelOptions(channel, context.Options)
                  PublishEventAsync(channel, context.Event, resolvedOptions)
                    → channel.PublishAsync(context.Event, resolvedOptions)
```

> **Important — middleware sees the raw event.**  
> Because enrichment happens in the terminal step, middleware receives the **un-enriched**
> `CloudEvent`.  This means middleware can freely set `id`, `time`, `source`, or custom
> extension attributes, and the built-in enrichment hooks will only fill in the values that
> are still absent once middleware has finished.

---

## Stage 1 — Channel name filtering

Before the `EventContext` is created, `FilterChannelsByName` narrows the set of target
channels based on `options`:

- When `options` implements `INamedChannelFilter` with a non-empty `ChannelName`, only
  channels whose `INamedEventPublishChannel.Name` matches (case-insensitive) are kept.
  Anonymous channels (those not implementing `INamedEventPublishChannel`) always pass
  through.
- When `options` is `null`, or `ChannelName` is empty/null, **all** channels are included.

`FilterChannelsByName` is `protected virtual` — override it in a subclass to implement
custom routing logic.

---

## Stage 2 — `EventContext` creation

An **async DI scope** is created for each publish call via
`IServiceProvider.CreateAsyncScope()`.  The `EventContext` is then initialised with:

| Property | Description |
|----------|-------------|
| `EventContext.Event` | The **raw** (not yet enriched) `CloudEvent`. Middleware and the terminal enrichment step may mutate or replace it. |
| `EventContext.Services` | A **scoped** `IServiceProvider` — a fresh scope per publish call, so scoped services (e.g. `IHttpContextAccessor`, `DbContext`) are resolved correctly. |
| `EventContext.CancellationToken` | The cancellation token for the current publish call. |
| `EventContext.Options` | The per-call `EventPublishOptions` passed by the caller (may be `null`). |
| `EventContext.Items` | A free-form `Dictionary<string, object?>` for passing arbitrary data between middleware steps within the same call. |

### `EventContext.Items` — sharing data between middleware

`Items` is the recommended way to pass intermediate results between middleware without
coupling the types to each other:

```csharp
// First middleware: stamp a correlation ID and store it for downstream steps.
public sealed class CorrelationMiddleware : IEventMiddleware
{
    private readonly ICorrelationAccessor _accessor;

    public CorrelationMiddleware(ICorrelationAccessor accessor)
        => _accessor = accessor;

    public async Task InvokeAsync(EventContext context, EventPublishDelegate next)
    {
        var correlationId = _accessor.CorrelationId ?? Guid.NewGuid().ToString();
        context.Items["correlationId"] = correlationId;

        var attr = CloudEventAttribute.CreateExtension("correlationid", CloudEventAttributeType.String);
        context.Event[attr] = correlationId;

        await next(context);
    }
}

// Second middleware: use the correlation ID set by the first one.
public sealed class AuditMiddleware : IEventMiddleware
{
    private readonly IAuditLog _audit;

    public AuditMiddleware(IAuditLog audit) => _audit = audit;

    public async Task InvokeAsync(EventContext context, EventPublishDelegate next)
    {
        var correlationId = context.Items.TryGetValue("correlationId", out var v) ? v as string : null;
        await next(context);
        await _audit.RecordAsync(context.Event.Type!, correlationId, context.CancellationToken);
    }
}
```

---

## Stage 3 — Middleware

Middleware is a **registration-time** concept: steps are added to the pipeline when the
publisher is configured via `EventPublisherBuilder.Use<TMiddleware>()` or
`EventPublisherBuilder.UseWhen<TMiddleware>(predicate)`.  The pipeline descriptor is
**frozen** the first time the publisher singleton is resolved from the container.  You
cannot add or remove middleware after that point.

### Core types

| Type | Role |
|------|------|
| `IEventMiddleware` | A composable step in the publish pipeline |
| `EventContext` | Carries the current `CloudEvent`, scoped `IServiceProvider`, cancellation token, per-call options, and an `Items` bag |
| `EventPublishDelegate` | `Task InvokeAsync(EventContext)` — represents the remainder of the pipeline (`next`) |
| `MiddlewareRegistration` | Describes a registered middleware step (type, activation arguments, optional predicate) |

### Registering middleware — `Use<TMiddleware>()`

Add middleware on the `EventPublisherBuilder` during DI registration:

```csharp
builder.Services
    .AddEventPublisher()
    .Use<CorrelationIdMiddleware>()
    .Use<MetricsMiddleware>();
```

You can pass explicit extra constructor arguments (forwarded to `ActivatorUtilities`):

```csharp
builder.Services
    .AddEventPublisher()
    .Use<AuditMiddleware>("orders");  // "orders" forwarded as an extra ctor arg
```

### Registering conditional middleware — `UseWhen<TMiddleware>(predicate)`

`UseWhen` registers a middleware that is **skipped** when the predicate returns `false`.
When skipped, the pipeline proceeds directly to the next registered step.

```csharp
builder.Services
    .AddEventPublisher()
    // Only stamp a trace ID when tracing is active for this request
    .UseWhen<TraceMiddleware>(ctx =>
        System.Diagnostics.Activity.Current is not null)
    // Only throttle events whose type starts with "com.example.bulk"
    .UseWhen<ThrottleMiddleware>(ctx =>
        ctx.Event.Type?.StartsWith("com.example.bulk") == true)
    .AddChannel<RabbitMqPublishChannel>();
```

> **Tip:** prefer `UseWhen` over an `if` block inside the middleware body when the
> predicate is a pure, allocation-free expression (e.g. checking a flag or an event-type
> prefix).  This avoids instantiating the middleware object when it would immediately
> short-circuit anyway.

### Ordering and activation rules

- Middleware runs in **registration order** — the first `Use<T>()` call becomes the
  **outermost** wrapper (closest to the caller).
- A **fresh middleware instance** is created for every publish call via `ActivatorUtilities`.
- Constructor dependencies are resolved from `EventContext.Services` at invocation time,
  which means **scoped services are fully supported**.
- The pipeline is compiled **once** when the publisher is first resolved; it cannot be
  changed after that point.

### Implementing `IEventMiddleware`

A middleware class implements `IEventMiddleware.InvokeAsync(EventContext, EventPublishDelegate)`:

```csharp
public sealed class CorrelationIdMiddleware : IEventMiddleware
{
    // Dependencies injected by ActivatorUtilities from the scoped EventContext.Services
    private readonly ICorrelationAccessor _accessor;

    public CorrelationIdMiddleware(ICorrelationAccessor accessor)
        => _accessor = accessor;

    public async Task InvokeAsync(EventContext context, EventPublishDelegate next)
    {
        if (!string.IsNullOrEmpty(_accessor.CorrelationId))
        {
            var attr = CloudEventAttribute.CreateExtension("correlationid", CloudEventAttributeType.String);
            context.Event[attr] = _accessor.CorrelationId;
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
    private readonly IEventDeduplicationStore _store;

    public DeduplicationMiddleware(IEventDeduplicationStore store)
        => _store = store;

    public async Task InvokeAsync(EventContext context, EventPublishDelegate next)
    {
        if (await _store.HasSeenAsync(context.Event.Id!, context.CancellationToken))
            return;   // stop here — do not forward to channels

        await next(context);

        // Mark as seen after successful delivery
        await _store.MarkAsync(context.Event.Id!, context.CancellationToken);
    }
}
```

### Observability middleware pattern

```csharp
public sealed class MetricsMiddleware : IEventMiddleware
{
    private readonly IMetricsCollector _metrics;

    public MetricsMiddleware(IMetricsCollector metrics) => _metrics = metrics;

    public async Task InvokeAsync(EventContext context, EventPublishDelegate next)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            await next(context);
            _metrics.RecordPublish(context.Event.Type!, sw.Elapsed, success: true);
        }
        catch
        {
            _metrics.RecordPublish(context.Event.Type!, sw.Elapsed, success: false);
            throw;
        }
    }
}
```

### Modifying `context.Options` in middleware

Middleware can replace or wrap the per-call options before the terminal dispatch step reads
them.  This is useful for injecting per-call routing decisions that depend on runtime state:

```csharp
public sealed class TenantRoutingMiddleware : IEventMiddleware
{
    private readonly ITenantContext _tenant;

    public TenantRoutingMiddleware(ITenantContext tenant) => _tenant = tenant;

    public async Task InvokeAsync(EventContext context, EventPublishDelegate next)
    {
        // Route to a tenant-specific named channel
        if (context.Options == null)
            context.Options = new NamedChannelPublishOptions(_tenant.ChannelName);

        await next(context);
    }
}
```

### Common middleware patterns

| Pattern | Description |
|---------|-------------|
| Enrichment | Add correlation IDs, tenant IDs, trace attributes to every event |
| Conditional enrichment | Use `UseWhen` to add attributes only when a condition is met |
| Validation / policy | Inspect `context.Event` and block selected publishes |
| Deduplication | Short-circuit when the same event ID has already been seen |
| Observability | Log, trace, and measure latency before/after `next(context)` |
| Options injection | Set or wrap `context.Options` to change channel routing at runtime |
| Data sharing | Use `context.Items` to pass intermediate results between steps |
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

## Stage 4 (terminal) — Enrichment

Once all middleware has run, the terminal step **enriches** the event by calling the four
`Set*` methods in sequence.

| Method | What it fills | Condition |
|--------|---------------|-----------|
| `SetEventId(CloudEvent)` | `id` attribute | Only when `id` is `null` and an `IEventIdGenerator` is registered |
| `SetTimeStamp(CloudEvent)` | `time` attribute | Only when `time` is `null` and an `IEventSystemTime` is registered |
| `SetSource(CloudEvent)` | `source` attribute | Only when `source` is `null` and `EventPublisherOptions.Source` is set |
| `SetAttributes(CloudEvent)` | Extension attributes from `EventPublisherOptions.Attributes` | Always; existing attributes are **overwritten** |

> **Middleware-set attributes vs. `EventPublisherOptions.Attributes`:**  
> Because `SetAttributes` runs after middleware and always overwrites existing values,
> any attribute key listed in `EventPublisherOptions.Attributes` will **overwrite** a value
> set by middleware.  If you need middleware to have the final say, do not list that
> attribute key in `EventPublisherOptions.Attributes`, or override `SetAttributes` in a
> subclass to change the merge strategy.

All four methods are `protected virtual` — override any of them in a subclass to customise
enrichment without touching the rest of the pipeline.  See
[Extending EventPublisher](event-publisher.md#extending-eventpublisher) for examples.

---

## Stage 4 (terminal) — Validation

`ValidateCloudEvent` checks the four mandatory CloudEvents 1.0 attributes **after**
middleware and enrichment have both run.

| Attribute | Auto-filled? | Must be caller-supplied when… |
|-----------|-------------|-------------------------------|
| `id` | ✅ via `IEventIdGenerator` | Never (always generated if absent) |
| `source` | ✅ from `EventPublisherOptions` | Options `Source` is `null` and caller did not set it |
| `type` | ❌ | Always — no default exists |
| `specversion` | ✅ by the CloudNative SDK (always `"1.0"`) | Never in practice |

If any required attribute is still absent, `ValidateCloudEvent` throws
`InvalidCloudEventException` **before** dispatching to any channel.  This exception is
always propagated regardless of the `ThrowOnErrors` option, because a structurally invalid
envelope is a programming error, not a transient delivery failure.

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

## Stage 4 (terminal) — Dispatch

The publisher's dispatch stage:

1. **Fan-out** over the already name-filtered channels (filtering was done in Stage 1).
2. **Per-channel options resolution** — `ResolveChannelOptions` extracts the compatible
   `EventPublishOptions` entry for the current channel (see
   [Per-call publish options](event-publisher.md#per-call-publish-options)).
3. **Delivery** — `PublishEventAsync(IEventPublishChannel, CloudEvent, EventPublishOptions?)`
   forwards the call to `channel.PublishAsync`.

If a channel throws and `ThrowOnErrors` is `false` (the default), the error is logged and
delivery continues to the remaining channels.  When `ThrowOnErrors` is `true` the exception
is wrapped in `EventPublishException` and re-thrown, stopping fan-out immediately.

---

## `MiddlewareRegistration`

`EventPublisherPipeline.MiddlewareRegistrations` exposes the ordered list of
`MiddlewareRegistration` entries that make up the pipeline.  Each entry carries:

| Property | Type | Description |
|----------|------|-------------|
| `MiddlewareType` | `Type` | The concrete `IEventMiddleware` implementation type. |
| `ActivationArguments` | `object[]` | Extra constructor arguments forwarded to `ActivatorUtilities`. |
| `Predicate` | `Func<EventContext, bool>?` | The condition predicate registered via `UseWhen`, or `null` when the middleware is unconditional. |
| `IsConditional` | `bool` | `true` when a predicate is attached (i.e. registered via `UseWhen`). |

This API is read-only and primarily useful for diagnostics and testing:

```csharp
// Resolving the pipeline (e.g. in a health-check or startup validation):
var pipeline = serviceProvider.GetRequiredService<EventPublisherPipeline>();
foreach (var reg in pipeline.MiddlewareRegistrations)
{
    Console.WriteLine($"{reg.MiddlewareType.Name} (conditional: {reg.IsConditional})");
}
```

---

## Middleware vs. subclassing

Middleware and subclassing are complementary extensibility mechanisms:

| Need | Prefer |
|------|--------|
| Add cross-cutting logic before/after publish | `Use<TMiddleware>()` on the builder |
| Skip middleware based on a runtime condition | `UseWhen<TMiddleware>(predicate)` on the builder |
| Short-circuit selected publishes | `Use<TMiddleware>()` on the builder |
| Resolve scoped services during publish | `Use<TMiddleware>()` on the builder |
| Share data between middleware steps | `EventContext.Items` |
| Change enrichment defaults globally | Subclass + override `Set*` methods |
| Change validation semantics | Subclass + override `ValidateCloudEvent` |
| Change channel selection / options matching | Subclass + override dispatch-related protected methods |

See [Extending EventPublisher](event-publisher.md#extending-eventpublisher) for full
subclassing examples.

---

## End-to-end example

The following example wires a correlation middleware, a conditional trace middleware, and
a metrics middleware into the default publisher pipeline:

```csharp
// Program.cs / Startup.cs
builder.Services
    .AddEventPublisher(opts =>
    {
        opts.Source = new Uri("https://myapp.example.com");
        opts.ThrowOnErrors = true;
    })
    // Unconditional — stamps a correlation ID on every event
    .Use<CorrelationIdMiddleware>()
    // Conditional — only stamps a W3C trace ID when a trace is active
    .UseWhen<TraceIdMiddleware>(ctx => System.Diagnostics.Activity.Current is not null)
    // Unconditional — measures latency for every publish call
    .Use<MetricsMiddleware>()
    .AddChannel<RabbitMqPublishChannel>();
```

Pipeline execution for a single `PublishEventAsync` call:

```
caller → CorrelationIdMiddleware.InvokeAsync
          → TraceIdMiddleware.InvokeAsync  (only if Activity.Current != null)
            → MetricsMiddleware.InvokeAsync
              → [terminal] SetEventId / SetTimeStamp / SetSource / SetAttributes
                         → ValidateCloudEvent
                         → RabbitMqPublishChannel.PublishAsync
```
