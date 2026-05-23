# OpenTelemetry Instrumentation

Hermodr provides built-in distributed tracing support through the `Hermodr.Publisher.OpenTelemetry` package. It instruments the publish pipeline with W3C trace context propagation, enabling end-to-end correlation across service boundaries via CloudEvents extension attributes.

## The problem

Events cross process boundaries but carry no trace context by default, making it impossible to correlate a published event with the originating request in a distributed trace. Without trace propagation, a span in the producer service is disconnected from any span in the consumer service.

## What it provides

| Capability | How it works |
|------------|-------------|
| **Producer span** | Creates an `Activity` (kind `Producer`) for every `PublishEventAsync` call |
| **Trace injection** | Writes W3C `traceparent` and `tracestate` as CloudEvents extension attributes on the event |
| **Consumer span** | Extracts `traceparent`/`tracestate` from incoming events and creates an `Activity` (kind `Consumer`) |
| **Handler participation** | Subscription handlers automatically see `Activity.Current` set to the consumer span |
| **Custom enrichment** | Optional `EnrichWithEvent` callback to add custom span tags |

## Installation

```bash
dotnet add package Hermodr.Publisher.OpenTelemetry
```

The package depends on `OpenTelemetry.Api` only — it does not require the full SDK. Configure your OpenTelemetry SDK separately to listen to the `Hermodr` activity source.

## Quick start

Enable instrumentation with a single builder call:

```csharp
builder.Services
    .AddEventPublisher(opts => opts.Source = new Uri("https://myapp.example.com"))
    .AddOpenTelemetry()
    .AddRabbitMq(opts => { opts.ConnectionString = "amqp://..."; opts.ExchangeName = "events"; })
    .AddSubscriptions(subs =>
    {
        subs.Subscribe("com.example.order.*", HandleOrderAsync);
    });
```

That's it. Every published event carries `traceparent` and `tracestate` extensions, and every subscription handler runs inside a consumer span linked to the producer span.

## How trace propagation works

### Publish side (inject)

```
Incoming request span ──► OpenTelemetryPublishMiddleware
                              │
                              ├─ StartActivity("publish com.example.order.created", Producer)
                              ├─ Inject traceparent + tracestate into CloudEvent extensions
                              ├─ await next(context)  ──► channel.PublishAsync(...)
                              └─ SetStatus(Ok) / SetStatus(Error)
```

The middleware creates a producer span, injects the W3C trace context into the CloudEvent, and propagates any exceptions to the span status.

### Subscription side (extract)

```
Received CloudEvent ──► OpenTelemetrySubscriptionMiddleware
                            │
                            ├─ Extract traceparent + tracestate from CloudEvent extensions
                            ├─ StartActivity("handle com.example.order.created", Consumer, parentContext)
                            ├─ await next(context)  ──► EventDispatcher ──► subscription handlers
                            └─ SetStatus(Ok) / SetStatus(Error)
```

The middleware extracts the remote parent context from the CloudEvent extensions and creates a consumer span. Subscription handlers see `Activity.Current` set to this span, so they can create child spans or attach baggage naturally.

### End-to-end trace

```
Service A (producer)                          Service B (consumer)
──────────────────                            ──────────────────
[HTTP request span]
  │
  └─► [publish com.example.order.created] ──► transport (RabbitMQ, ASB, ...)
        traceparent: 00-{traceId}-{spanA}-01       │
                                                   ▼
                                            [handle com.example.order.created]
                                              traceparent matches → same traceId
                                              parentSpanId = spanA
```

## Configuration

### Full instrumentation (default)

```csharp
builder.Services
    .AddEventPublisher()
    .AddOpenTelemetry(opts =>
    {
        opts.ActivitySourceName = "MyService.Hermodr";
        opts.RecordException = true;
        opts.EnrichWithEvent = (activity, @event) =>
        {
            activity.SetTag("tenant.id", GetTenantId(@event));
        };
    });
```

### Publisher-only instrumentation

Use when you only produce events and don't have in-process subscriptions:

```csharp
builder.Services
    .AddEventPublisher()
    .AddOpenTelemetryPublisherInstrumentation();
```

### Subscription-only instrumentation

Use when you only consume events (e.g., a worker service that receives events from a broker):

```csharp
builder.Services
    .AddEventPublisher()
    .AddOpenTelemetrySubscriptionInstrumentation();
```

### Options reference

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `ActivitySourceName` | `string` | `"Hermodr"` | Name of the `ActivitySource` emitted by the middleware |
| `InstrumentPublisher` | `bool` | `true` | Whether to register the publish-side middleware |
| `InstrumentSubscription` | `bool` | `true` | Whether to register the subscription-side middleware |
| `RecordException` | `bool` | `true` | Whether to attach exception details to spans on error |
| `EnrichWithEvent` | `Action<Activity, CloudEvent>?` | `null` | Callback invoked after span creation to add custom tags |

## CloudEvents extension attributes

The middleware uses standard W3C extension attribute names on the CloudEvent:

| Extension | Type | Description |
|-----------|------|-------------|
| `traceparent` | `string` | W3C Trace Context traceparent value (e.g., `00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01`) |
| `tracestate` | `string` | W3C Trace Context tracestate value (only set when present on the activity) |

These are standard CloudEvents extension attributes — any CloudEvents-compatible consumer can read them, even if it doesn't use Hermodr.

## Span tags

Every span created by the instrumentation includes these tags:

| Tag | Value | Description |
|-----|-------|-------------|
| `event.type` | The CloudEvent type | Identifies the event being published or handled |
| `event.id` | The CloudEvent ID (when set) | Unique event identifier |
| `messaging.system` | `"hermodr"` | Identifies the messaging framework |
| `messaging.operation` | `"publish"` or `"receive"` | Distinguishes producer from consumer spans |

Custom tags can be added via the `EnrichWithEvent` callback.

## Accessing the activity in subscription handlers

Subscription handlers can access the current consumer span through `Activity.Current`:

```csharp
subs.Subscribe("com.example.order.*", async (evt, ct) =>
{
    // Activity.Current is the consumer span created by OpenTelemetrySubscriptionMiddleware
    var currentActivity = Activity.Current;

    // Create a child span for your handler logic
    using var handlerSpan = new ActivitySource("MyService")
        .StartActivity("process-order", ActivityKind.Internal);

    // ... handler logic ...
});
```

The middleware also stores the consumer `Activity` reference in `EventContext.Items` under the key `"Hermodr.Activity"` for middleware that runs between the subscription middleware and the dispatcher.

## OpenTelemetry SDK configuration

The package only emits activities — you need to configure the OpenTelemetry SDK to collect and export them:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource("Hermodr")              // or your custom ActivitySourceName
        .AddAspNetCoreInstrumentation()
        .AddOtlpExporter());
```

## Middleware ordering

The OpenTelemetry middleware follows the standard registration order:

```
caller → OpenTelemetryPublishMiddleware
          → OpenTelemetrySubscriptionMiddleware
            → EventDispatcher (if AddSubscriptions was called)
              → [your custom middleware]
                → [terminal: enrich, validate, dispatch to channels]
```

Place custom middleware that depends on trace context **after** `AddOpenTelemetry()` so it runs inside the producer/consumer span:

```csharp
builder.Services
    .AddEventPublisher()
    .AddOpenTelemetry()
    .Use<AuditMiddleware>()     // runs inside the producer span
    .AddChannel<RabbitMqPublishChannel>();
```
