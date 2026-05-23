# OrderService — OpenTelemetry Instrumentation

This sample demonstrates the **OpenTelemetry** integration in `Hermodr.Publisher.OpenTelemetry`.

## What it demonstrates

| Concern | Implementation |
|---------|---------------|
| Publisher spans | `OpenTelemetryPublishMiddleware` creates a producer span for each publish operation |
| Trace injection | W3C `traceparent`/`tracestate` are injected as CloudEvent extension attributes |
| Trace extraction | `OpenTelemetrySubscriptionMiddleware` extracts trace context from incoming events |
| Metrics collection | `MetricsMiddleware` collects duration, count, and error metrics via `System.Diagnostics.Metrics` |
| Custom enrichment | `EnrichWithEvent` callback adds domain-specific tags to spans |
| Console export | OpenTelemetry SDK exports traces and metrics to the console for demonstration |

## Flow

```
ProcessOrderWorkflow (parent activity)
    │
    ▼
hermodr.publish.order.submitted (producer span)
    │
    ├── injects traceparent/tracestate as CloudEvent extensions
    │
    ▼
LoggingChannel receives the CloudEvent with trace context
```

## Running the sample

```bash
cd samples/opentelemetry/OrderService.OpenTelemetry
dotnet run
```

Expected output:

1. A parent `ProcessOrderWorkflow` activity is started
2. The publish creates a `hermodr.publish.order.submitted` producer span
3. The trace context is injected into the CloudEvent as extension attributes
4. The `LoggingChannel` receives the event and prints the traceparent/tracestate values
5. OpenTelemetry console exporter outputs the span details
6. Metrics are exported showing publish duration and counts

## Configuration

| Key | Default | Description |
|-----|---------|-------------|
| `Sample:Source` | `https://samples.deveel.events/opentelemetry` | CloudEvent source used by the publisher |
| `Sample:DataSchemaBaseUri` | `https://samples.deveel.events/schema/` | Base URI used to derive the event `dataschema` |

## Key API usage

```csharp
services.AddEventPublisher()
    .UseOpenTelemetry(opts =>
    {
        opts.ActivitySourceName = "Hermodr.Sample";
        opts.RecordException = true;
        opts.EnrichWithEvent = (activity, cloudEvent) =>
        {
            activity.SetTag("order.customer_id", order.CustomerId);
        };
    })
    .AddRabbitMq(...);
```

To disable one side of the instrumentation, use the options:

```csharp
.UseOpenTelemetry(opts => opts.InstrumentSubscription = false) // publisher only
.UseOpenTelemetry(opts => opts.InstrumentPublisher = false)    // subscription only
```
