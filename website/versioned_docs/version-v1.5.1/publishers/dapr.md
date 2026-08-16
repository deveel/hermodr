# Dapr Channel

The `Hermodr.Publisher.Dapr` package publishes CloudEvents through the [Dapr pub/sub building block](https://docs.dapr.io/developing-applications/building-blocks/pubsub/pubsub-overview/) on top of `DaprClient`. Dapr acts as a portable, sidecar-driven eventing abstraction over many brokers (Kafka, RabbitMQ, Azure Service Bus, NATS, Redis, ...) — the underlying broker is selected through [Dapr component configuration](https://docs.dapr.io/operations/components/), keeping the publish code agnostic of the transport.

## Installation

```bash
dotnet add package Hermodr.Publisher.Dapr
```

## Registration

### Inline configuration

```csharp
using Hermodr;

builder.Services
    .AddEventPublisher()
    .AddDapr(options =>
    {
        options.PubSubName = "hermodr-pubsub";
        options.Topic      = "events";
    });
```

### From `appsettings.json`

```csharp
builder.Services
    .AddEventPublisher()
    .AddDapr("Events:Dapr");
```

```json
// appsettings.json
{
  "Events": {
    "Dapr": {
      "PubSubName": "hermodr-pubsub",
      "Topic": "events"
    }
  }
}
```

## Options reference

`DaprPublishOptions`

Delivery settings (nullable — `null` in a per-call override inherits the channel default):

| Property | Type | Effective default | Description |
|----------|------|-------------------|-------------|
| `PubSubName` | `string` | _(required)_ | Name of the Dapr pub/sub component the events are published to (the `pubsubname` argument of the `PublishEvent` building block) |
| `Topic` | `string?` | `null` | Topic on the pub/sub component; when `null` the topic is resolved from `TopicSelector` and, failing that, from the CloudEvent `subject` attribute |
| `TopicSelector` | `Func<CloudEvent, string?>?` | `null` | Delegate selecting the topic at publish time; overrides `Topic` when it returns a non-`null` value |
| `MapAttributesToMetadata` | `bool?` | `true` | Whether the CloudEvent attributes are projected onto Dapr metadata (see [Metadata mapping](#metadata-mapping)) |
| `MetadataPrefix` | `string?` | `"ce-"` | Prefix applied to CloudEvent attribute names when projected onto Dapr metadata |
| `DaprClientName` | `string?` | `null` | Optional name used to resolve a named `DaprClient` through the registered `IDaprClientBuilder`; when `null` the default (unnamed) client is used |
| `ChannelName` | `string?` | `null` | Logical name for [named channel](named-channels.md) scenarios |
| `ScheduleDeliveryAt` | `DateTimeOffset?` | `null` | Optional scheduled (deferred) delivery time, consistent with the other channels |

## Topic resolution

The channel resolves the target topic in this order:

1. `DaprPublishOptions.Topic` — when set, always used.
2. `DaprPublishOptions.TopicSelector` — when it returns a non-`null` value.
3. The CloudEvent `subject` attribute — final fallback.

When none of the three yields a topic, publishing throws `DaprPublishException` at publish time.

```csharp
builder.Services
    .AddEventPublisher()
    .AddDapr(options =>
    {
        options.PubSubName = "hermodr-pubsub";
        options.TopicSelector = e => e.Type?.Split('.').Last().ToLowerInvariant();
    });
```

## Metadata mapping

The CloudEvent is serialised in **structured mode** (canonical CloudEvents JSON envelope) and published verbatim through `PublishByteEventAsync`, so consumers receive the canonical envelope regardless of the underlying broker.

When `MapAttributesToMetadata` is in effect (the default), the CloudEvent attributes are additionally projected onto Dapr metadata as `<prefix><attribute-name>` keys (default prefix `"ce-"`), e.g. `ce-id`, `ce-type`, `ce-source`, `ce-time`, `ce-datacontenttype`, `ce-subject`, `ce-specversion`, plus any extension attributes. This lets broker-side routing and subscription filter rules match on CloudEvent attributes without deserialising the payload.

```csharp
builder.Services
    .AddEventPublisher()
    .AddDapr(options =>
    {
        options.PubSubName           = "hermodr-pubsub";
        options.Topic                = "events";
        options.MapAttributesToMetadata = true;
        options.MetadataPrefix       = "ce-";
    });
```

## Sidecar configuration

A Dapr pub/sub component is required for the sidecar the channel publishes through. Create a component file in the Dapr components directory:

```yaml
# components/pubsub.yaml
apiVersion: dapr.io/v1alpha1
kind: Component
metadata:
  name: hermodr-pubsub
spec:
  type: pubsub.redis
  version: v1
  metadata:
  - name: redisHost
    value: redis:6379
  - name: redisPassword
    value: ""
```

The `metadata.name` must match the `PubSubName` configured on the channel.

## Typed channel

Use `AddDapr<TEvent>()` to register a channel that receives **only** events whose data class is `TEvent`. At construction time the typed channel (`DaprPublishChannel<TEvent>`) merges the general `DaprPublishOptions` with the type-specific `DaprPublishOptions<TEvent>`: non-`null` typed values win; `null` values fall back to the base defaults.

```csharp
builder.Services
    .AddEventPublisher()
    // General catch-all Dapr channel
    .AddDapr(opts =>
    {
        opts.PubSubName = "hermodr-pubsub";
        opts.Topic      = "events";
    })
    // OrderPlaced publishes to a dedicated topic
    .AddDapr<OrderPlaced>(opts =>
    {
        opts.Topic = "orders";
        // PubSubName inherited from base
    });
```

From configuration:

```csharp
builder.Services
    .AddEventPublisher()
    .AddDapr("Events:Dapr")
    .AddDapr<OrderPlacedData>("Events:Dapr:Orders");
```

```json
{
  "Events": {
    "Dapr": {
      "PubSubName": "hermodr-pubsub",
      "Topic": "events",
      "Orders": {
        "Topic": "orders"
      }
    }
  }
}
```

See [Typed Channels](typed-channels.md) for the full merge semantics.

## DaprClient resolution

By default the channel builds a `DaprClient` through the standard Dapr SDK surface. The host is expected to have called `AddDaprClient()` (from the Dapr SDK); when it has not, a default client is constructed on demand.

For advanced scenarios (mTLS, token credentials, named sidecars), replace the built-in client resolution by registering a custom `IDaprClientBuilder`. The default implementation resolves the unnamed `DaprClient` from DI and a named client keyed by `DaprPublishOptions.DaprClientName` when registered through keyed services:

```csharp
builder.Services
    .AddEventPublisher()
    .AddDapr(options =>
    {
        options.PubSubName    = "hermodr-pubsub";
        options.DaprClientName = "mtls-sidecar";
    });

builder.Services.AddKeyedSingleton<DaprClient>("mtls-sidecar",
    (_, _) => new DaprClientBuilder()
        .UseHttpEndpoint("https://my-sidecar.example.com")
        .UseGrpcChannelAddress("my-sidecar.example.com:50001")
        .UseDaprApiToken("...")
        .Build());
```

To fully replace the resolution mechanism:

```csharp
public sealed class MyDaprClientBuilder : IDaprClientBuilder
{
    public DaprClient CreateClient(string? name = null)
    {
        // ... custom DaprClient construction per name
        return new DaprClientBuilder().Build();
    }
}

builder.Services.AddSingleton<IDaprClientBuilder, MyDaprClientBuilder>();
```

## Error handling

Transport failures are wrapped in `DaprPublishException` (an `EventPublishException`), so the framework's [error-handling pipeline](error-handling.md) — including the [dead-letter channel](reliability/dead-letter/README.md) and the [delivery log](reliability/delivery-log/README.md) — engages automatically.

## Observability

The channel emits OpenTelemetry spans and tags consistent with the other transport channels:

- `messaging.system` = `dapr`
- `messaging.destination` = the resolved topic
- `messaging.dapr.pubsubname` = the configured pub/sub component

Spans are produced from the central `HermodrDiagnostics.ActivitySource`, so the standard [OpenTelemetry instrumentation](opentelemetry.md) applies unchanged.

## Per-delivery options

Pass a `DaprPublishOptions` instance as the second argument to `PublishAsync`. Only the properties you set (non-`null`) override the channel default — all others fall back to the values configured at registration time.

```csharp
using Hermodr;
using Dapr.Client;

// Resolve the concrete channel directly from DI.
var daprChannel = serviceProvider.GetRequiredService<DaprPublishChannel>();

await daprChannel.PublishAsync(@event, new DaprPublishOptions
{
    Topic = "promotions", // override the topic for this delivery only
});
```

## Related pages

- [Publisher Channels Overview](README.md)
- [Typed Channels](typed-channels.md)
- [Named Channels](named-channels.md)
- [Event Publisher](../concepts/event-publisher.md)