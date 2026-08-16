# Webhook Channel

The `Hermodr.Publisher.Webhook` package delivers `CloudEvent` instances over HTTP to a configured endpoint URL, with optional HMAC request signing, exponential-backoff retries, and pluggable serialisers.

## Installation

```bash
dotnet add package Hermodr.Publisher.Webhook
```

## Registration

### Inline configuration

```csharp
using Hermodr;

builder.Services
    .AddEventPublisher()
    .AddWebhooks(options =>
    {
        options.EndpointUrl      = "https://partner.example.com/events";
        options.SigningSecret     = "s3cr3t";
        options.SignatureAlgorithm = WebhookSignatureAlgorithm.HmacSha256;
        options.MaxRetryCount     = 3;
    });
```

### From `appsettings.json`

```csharp
builder.Services
    .AddEventPublisher()
    .AddWebhooks("Events:Webhook");
```

```json
// appsettings.json
{
  "Events": {
    "Webhook": {
      "EndpointUrl": "https://partner.example.com/events",
      "SigningSecret": "s3cr3t",
      "SignatureAlgorithm": "HmacSha256",
      "MaxRetryCount": 3,
      "RetryDelay": "00:00:01",
      "RetryBackoffMultiplier": 2.0,
      "RequestTimeout": "00:00:30"
    }
  }
}
```

## Options reference

`WebhookPublishOptions`

Delivery settings (nullable — `null` in a per-call override inherits the channel default):

| Property | Type | Effective default | Description |
|----------|------|-------------------|-------------|
| `EndpointUrl` | `string?` | _(required)_ | URL of the webhook endpoint |
| `SigningSecret` | `string?` | `null` | Shared secret for HMAC signing; no signature header is sent when omitted |
| `SignatureAlgorithm` | `WebhookSignatureAlgorithm?` | `HmacSha256` | HMAC algorithm used to sign the body |
| `MessageFormat` | `string?` | `"json"` | Serialisation format. Use `EventMessageFormat` constants (for built-ins: `"json"`, `"xml"`, `"cloudevents+json"`, `"cloudevents+xml"`, `"cloudevents+binary"`) or a custom serializer format key |
| `MaxRetryCount` | `int?` | `3` | Maximum delivery attempts; `0` disables retries |
| `RetryDelay` | `TimeSpan?` | 1 s | Initial delay between retries |
| `RetryBackoffMultiplier` | `double?` | `2.0` | Multiplier for exponential backoff |
| `RequestTimeout` | `TimeSpan?` | 30 s | Timeout per individual HTTP request |
| `AdditionalHeaders` | `IDictionary<string, string>` | `{}` | Extra HTTP headers merged into every request; per-call entries win on key collision |
| `Discovery` | `WebhookDiscoveryOptions?` | `null` | CloudEvents WebHooks abuse-protection handshake (lazy `OPTIONS` pre-flight + `WebHook-Request-Origin` on every POST); see [WebHook discovery](#webhook-discovery-abuse-protection) below |

Channel-structural settings (always taken from the channel-level defaults; ignored in per-call overrides):

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `SignatureHeaderName` | `string` | `X-Webhook-Signature` | HTTP header carrying the computed signature |
| `SignatureAlgorithmHeaderName` | `string?` | `X-Webhook-Signature-Algorithm` | Header advertising the algorithm used; set to `null` to suppress |
| `DeliveryIdHeaderName` | `string` | `X-Webhook-Delivery` | Header carrying a unique delivery identifier |
| `EventTypeHeaderName` | `string` | `X-Webhook-Event` | Header carrying the event type |
| `TimestampHeaderName` | `string` | `X-Webhook-Timestamp` | Unix-epoch timestamp header (used in signature payload to prevent replay attacks) |
| `RetryableStatusCodes` | `ISet<int>` | 429, 500, 502, 503, 504 | HTTP status codes that trigger a retry |
| `HttpClientName` | `string?` | `null` | Named `HttpClient` resolved from `IHttpClientFactory`; defaults to the internal channel name |

`TimestampHeaderName` carries the Unix timestamp used in signature computation.
The channel uses `CloudEvent.time` when present; otherwise it falls back to `IEventSystemTime.UtcNow`.
This keeps signatures deterministic in tests when you replace the clock with `UseSystemTime<TClock>()`.

## Typed channel

Use `AddWebhooks<TEvent>()` to register a channel that receives **only** events whose data class is `TEvent`.  At construction time the typed channel (`WebhookPublishChannel<TEvent>`) merges the general `WebhookPublishOptions` with the type-specific `WebhookPublishOptions<TEvent>`: non-`null` typed values win; `null` values fall back to the base defaults.  `AdditionalHeaders` are merged at the dictionary level — typed entries win on key collision.  Channel-structural properties (`SignatureHeaderName`, `DeliveryIdHeaderName`, `RetryableStatusCodes`, …) are **always** taken from the base options and cannot be overridden per event type.

```csharp
builder.Services
    .AddEventPublisher()
    // General catch-all webhook
    .AddWebhooks(opts =>
    {
        opts.EndpointUrl       = "https://partner.example.com/events";
        opts.SigningSecret      = "shared-secret";
        opts.MaxRetryCount      = 3;
        opts.SignatureAlgorithm = WebhookSignatureAlgorithm.HmacSha256;
    })
    // OrderPlaced delivers to a dedicated endpoint with its own secret
    .AddWebhooks<OrderPlaced>(opts =>
    {
        opts.EndpointUrl  = "https://orders.example.com/hooks";
        opts.SigningSecret = "order-secret";
        // MaxRetryCount and SignatureAlgorithm inherited from base
    });
```

From configuration:

```csharp
builder.Services
    .AddEventPublisher()
    .AddWebhooks("Events:Webhook")
    .AddWebhooks<OrderPlacedData>("Events:Webhook:Orders");
```

```json
{
  "Events": {
    "Webhook": {
      "EndpointUrl": "https://partner.example.com/events",
      "SigningSecret": "shared-secret",
      "MaxRetryCount": 3,
      "Orders": {
        "EndpointUrl": "https://orders.example.com/hooks",
        "SigningSecret": "order-secret"
      }
    }
  }
}
```

See [Typed Channels](typed-channels.md) for the full merge semantics and further examples.

## Signature algorithms

| Value | Algorithm | Note |
|-------|-----------|------|
| `HmacSha256` | HMAC-SHA256 | **Recommended** default |
| `HmacSha384` | HMAC-SHA384 | |
| `HmacSha512` | HMAC-SHA512 | |
| `HmacSha1` | HMAC-SHA1 | Deprecated; included for legacy compatibility |

The signature is computed over `<timestamp>.<body>` and sent in the configured signature header.

## Message formats

Use `EventMessageFormat` constants when selecting a built-in format.

| `MessageFormat` value | Content-Type | Description |
|-----------------------|--------------|-------------|
| `"json"` (`EventMessageFormat.Json`) | `application/json` | Plain JSON payload (default) |
| `"xml"` (`EventMessageFormat.Xml`) | `application/xml` | Plain XML payload |
| `"cloudevents+json"` (`EventMessageFormat.CloudEventsJson`) | `application/cloudevents+json` | Full CloudEvents JSON envelope |
| `"cloudevents+xml"` (`EventMessageFormat.CloudEventsXml`) | `application/cloudevents+xml` | Full CloudEvents XML envelope |
| `"cloudevents+binary"` (`EventMessageFormat.CloudEventsBinary`) | event's `datacontenttype` (fallback `application/json`) | CloudEvents [binary HTTP content mode](https://github.com/cloudevents/spec/blob/main/cloudevents/bindings/http-protocol-binding.md#31-binary-content-mode) — `ce-*` attribute headers + data-only body; single-event only (batch throws `NotSupportedException`) |

In binary content mode the channel maps every CloudEvent context attribute
(including extensions) to a `ce-*` HTTP header, sets the request `Content-Type`
to the event's `datacontenttype`, and sends the raw `data` as the body. This
lets receivers compliant with the CloudEvents HTTP binding (Azure Event Grid,
Knative Eventing, etc.) route on attributes without deserialising the body,
and allows `data` to be any content type (e.g. `application/protobuf`).

```csharp
builder.Services
    .AddEventPublisher()
    .AddWebhooks(options =>
    {
        options.EndpointUrl   = "https://partner.example.com/events";
        options.MessageFormat = EventMessageFormat.CloudEventsBinary;
    });
```

> **Batch + binary:** the CloudEvents HTTP binding defines no binary batch
> mode; `PublishBatchAsync` throws `NotSupportedException` when the effective
> format is `cloudevents+binary`. Use `cloudevents+json` for batched delivery.

## WebHook discovery (abuse protection)

When the delivery target supports and requires
[CloudEvents HTTP WebHooks](https://github.com/cloudevents/spec/blob/main/cloudevents/http-webhook.md)
abuse protection, the channel can perform an `OPTIONS` validation handshake
before the first delivery and attach the `WebHook-Request-Origin` header to
every subsequent POST.  Enable it by setting `WebhookPublishOptions.Discovery`
to a `WebhookDiscoveryOptions` instance.

### `WebhookDiscoveryOptions`

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `RequestOrigin` | `string` | _(required)_ | DNS name identifying the sending system, sent as `WebHook-Request-Origin` on the `OPTIONS` request and on every delivery POST |
| `RequestCallback` | `string?` | `null` | Optional HTTPS URL advertised via `WebHook-Request-Callback`; the receiver may call it asynchronously to grant permission. The channel does **not** host this endpoint. |
| `RequestRate` | `int?` | `null` | Desired request rate (requests per minute), sent via `WebHook-Request-Rate`; the receiver may grant a different rate |
| `RequestTimeout` | `TimeSpan?` | `null` | Timeout for the `OPTIONS` request; falls back to `RequestTimeout` (default 30 s) when `null` |
| `RequireSuccessfulHandshake` | `bool` | `false` | `false` (best-effort): a refused or faulted handshake is logged and the delivery proceeds. `true` (strict): throws `WebhookHandshakeException` and suppresses the POST. |

### How the handshake works

1. Before the first delivery to an endpoint, the channel issues an `OPTIONS`
   request carrying `WebHook-Request-Origin` (and, when set,
   `WebHook-Request-Callback` / `WebHook-Request-Rate`).
2. The target grants permission by returning `WebHook-Allowed-Origin`
   (the origin name, or `*` for all origins) and optionally
   `WebHook-Allowed-Rate` (an integer, or `*` for unlimited).  The granted
   permission is cached per endpoint and reused for subsequent deliveries;
   concurrent first deliveries share a single round-trip.
3. After a successful handshake, `WebHook-Request-Origin` is attached to
   every delivery POST, matching the value used in the handshake (spec §2.1).
4. The granted `WebHook-Allowed-Rate` is tagged on the publish
   [`Activity`](https://learn.microsoft.com/dotnet/api/system.diagnostics.activity)
   (`webhook.allowed_origin`, `webhook.allowed_rate`) and cached as a
   `WebhookDiscoveryPermission`, but **not auto-enforced** — the host can
   read it to apply its own throttling policy.

### Best-effort vs. strict

- `RequireSuccessfulHandshake = false` (default) — a refused handshake (no
  `WebHook-Allowed-Origin`) or a faulted request is logged and the delivery
  proceeds.  Use this when the target may not implement the handshake.
- `RequireSuccessfulHandshake = true` — a refused or faulted handshake throws
  `WebhookHandshakeException` (carrying the `EndpointUrl` and, when
  applicable, the `StatusCode`) and the delivery POST is not sent.

### `Retry-After` on 429

When a delivery returns `429 Too Many Requests` with a
[`Retry-After`](https://developer.mozilla.org/docs/Web/HTTP/Headers/Retry-After)
header (delta-seconds or HTTP-date), the channel uses it as the delay before
the next retry attempt, overriding the configured exponential backoff for
that attempt (spec §2.2).  An absent or unparseable `Retry-After` falls back
to the normal backoff.

### Configuration

```csharp
builder.Services
    .AddEventPublisher()
    .AddWebhooks(options =>
    {
        options.EndpointUrl   = "https://partner.example.com/events";
        options.SigningSecret  = "s3cr3t";
        options.Discovery = new WebhookDiscoveryOptions
        {
            RequestOrigin              = "eventemitter.example.com",
            RequestRate                 = 120,
            RequireSuccessfulHandshake = true
        };
    });
```

```json
{
  "Events": {
    "Webhook": {
      "EndpointUrl": "https://partner.example.com/events",
      "SigningSecret": "s3cr3t",
      "Discovery": {
        "RequestOrigin": "eventemitter.example.com",
        "RequestRate": 120,
        "RequireSuccessfulHandshake": true
      }
    }
  }
}
```

> The publisher does **not** host the `WebHook-Request-Callback` endpoint:
> it only advertises the callback URL so the receiver (or an administrator)
> can grant permission out-of-band.  Hosting a callback receiver belongs to
> the subscription-management layer.

## Per-delivery options

Pass a `WebhookPublishOptions` instance as the second argument to `PublishAsync`.  Only the properties you set (non-`null`) override the channel default — all others fall back to the values configured at registration time.  `AdditionalHeaders` are merged: per-call entries win on key collision.

```csharp
using Hermodr;

// Resolve the concrete channel directly from DI.
var webhookChannel = serviceProvider.GetRequiredService<WebhookPublishChannel>();

// Override endpoint and secret for this delivery only;
// everything else (MaxRetryCount, SignatureAlgorithm, …) is inherited.
await webhookChannel.PublishAsync(@event, new WebhookPublishOptions
{
    EndpointUrl       = "https://dynamic-endpoint.example.com/hook",
    SigningSecret      = "per-tenant-secret",
    SignatureAlgorithm = WebhookSignatureAlgorithm.HmacSha512
});
```

You can also supply overrides through the non-generic `IEventPublishChannel` interface — casting the options to `EventPublishOptions` works because `WebhookPublishOptions` inherits from it:

```csharp
IEventPublishChannel channel = serviceProvider.GetRequiredService<WebhookPublishChannel>();
await channel.PublishAsync(@event, new WebhookPublishOptions { EndpointUrl = "https://..." });
```

## Batch delivery

The channel implements `IBatchEventPublishChannel` for dispatching multiple events in a single HTTP call:

```csharp
var batchChannel = serviceProvider.GetRequiredService<IBatchEventPublishChannel>();

await batchChannel.PublishBatchAsync(events, new WebhookPublishOptions
{
    EndpointUrl = "https://partner.example.com/events/batch"
});
```

## Custom serialiser

Register a custom `IEventSerializer` by adding it to the service collection as a singleton enumerable entry for `IEventSerializer`.  The channel picks it up by matching its `Format` key.

```csharp
public class ProtobufEventSerializer : IEventSerializer
{
    public string Format => "protobuf";
    public string ContentType => "application/x-protobuf";
    public string BatchContentType => "application/x-protobuf";

    public byte[] Serialize(CloudEvent @event)
    {
        // ... protobuf serialisation
        throw new NotImplementedException();
    }

    public byte[] SerializeBatch(IReadOnlyList<CloudEvent> events)
    {
        // ... batch serialisation
        throw new NotImplementedException();
    }
}
```

```csharp
builder.Services
    .AddEventPublisher()
    .AddWebhooks(options => options.MessageFormat = "protobuf");

// Register the custom serializer so the channel discovers it via DI
builder.Services.AddSingleton<IEventSerializer, ProtobufEventSerializer>();
```

## Custom signature provider

Implement `IWebhookSignatureProvider` and register it as a singleton:

```csharp
public class Ed25519SignatureProvider : IWebhookSignatureProvider
{
    public static readonly Ed25519SignatureProvider Default = new();
    public WebhookSignatureAlgorithm Algorithm => (WebhookSignatureAlgorithm)100; // custom value
    public string AlgorithmName => "ed25519";

    public string ComputeSignature(byte[] payload, long timestamp, string secret)
    {
        // ... Ed25519 signing
        throw new NotImplementedException();
    }
}
```

```csharp
builder.Services
    .AddEventPublisher()
    .AddWebhooks(options => { /* ... */ });

// Register the custom provider so the channel discovers it via DI
builder.Services.AddSingleton<IWebhookSignatureProvider, Ed25519SignatureProvider>();
```

## Related pages

- [Publisher Channels Overview](README.md)
- [Typed Channels](typed-channels.md)
- [Event Publisher](../concepts/event-publisher.md)

