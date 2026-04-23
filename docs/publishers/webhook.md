# Webhook Channel

The `Deveel.Events.Publisher.Webhook` package delivers `CloudEvent` instances over HTTP to a configured endpoint URL, with optional HMAC request signing, exponential-backoff retries, and pluggable serialisers.

## Installation

```bash
dotnet add package Deveel.Events.Publisher.Webhook
```

## Registration

### Inline configuration

```csharp
using Deveel.Events;

builder.Services
    .AddEventPublisher()
    .UseWebhook(options =>
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
    .UseWebhook("Events:Webhook");
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

`WebhookEventPublishChannelOptions`

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `EndpointUrl` | `string` | `""` | URL of the webhook endpoint |
| `SigningSecret` | `string?` | `null` | Shared secret for HMAC signing; no signature header is sent when omitted |
| `SignatureAlgorithm` | `WebhookSignatureAlgorithm` | `HmacSha256` | HMAC algorithm used to sign the body |
| `SignatureHeaderName` | `string` | `X-Webhook-Signature` | HTTP header carrying the computed signature |
| `SignatureAlgorithmHeaderName` | `string?` | `X-Webhook-Signature-Algorithm` | Header advertising the algorithm used; set to `null` to suppress |
| `DeliveryIdHeaderName` | `string` | `X-Webhook-Delivery` | Header carrying a unique delivery identifier |
| `EventTypeHeaderName` | `string` | `X-Webhook-Event` | Header carrying the event type |
| `TimestampHeaderName` | `string` | `X-Webhook-Timestamp` | Unix-epoch timestamp header (used in signature payload to prevent replay attacks) |
| `AdditionalHeaders` | `IDictionary<string, string>` | `{}` | Extra HTTP headers added to every request |
| `MessageFormat` | `string` | `"json"` | Serialisation format: `"json"`, `"xml"`, `"cloudevents+json"`, `"cloudevents+xml"` |
| `MaxRetryCount` | `int` | `3` | Maximum delivery attempts; `0` disables retries |
| `RetryDelay` | `TimeSpan` | 1 s | Initial delay between retries |
| `RetryBackoffMultiplier` | `double` | `2.0` | Multiplier for exponential backoff |
| `RetryableStatusCodes` | `ISet<int>` | 429, 500, 502, 503, 504 | HTTP status codes that trigger a retry |
| `RequestTimeout` | `TimeSpan` | 30 s | Timeout per individual HTTP request |
| `HttpClientName` | `string?` | `null` | Named `HttpClient` resolved from `IHttpClientFactory`; defaults to the internal channel name |

## Signature algorithms

| Value | Algorithm | Note |
|-------|-----------|------|
| `HmacSha256` | HMAC-SHA256 | **Recommended** default |
| `HmacSha384` | HMAC-SHA384 | |
| `HmacSha512` | HMAC-SHA512 | |
| `HmacSha1` | HMAC-SHA1 | Deprecated; included for legacy compatibility |

The signature is computed over `<timestamp>.<body>` and sent in the configured signature header.

## Message formats

| `MessageFormat` value | Content-Type | Description |
|-----------------------|--------------|-------------|
| `"json"` | `application/json` | Plain JSON payload (default) |
| `"xml"` | `application/xml` | Plain XML payload |
| `"cloudevents+json"` | `application/cloudevents+json` | Full CloudEvents JSON envelope |
| `"cloudevents+xml"` | `application/cloudevents+xml` | Full CloudEvents XML envelope |

## Per-delivery options

The channel also implements `IEventPublishChannel<WebhookPublishOptions>`, allowing you to override options per event:

```csharp
using Deveel.Events;

// Resolve via DI
var webhookChannel = serviceProvider.GetRequiredService<IEventPublishChannel<WebhookPublishOptions>>();

await webhookChannel.PublishAsync(@event, new WebhookPublishOptions
{
    EndpointUrl       = "https://dynamic-endpoint.example.com/hook",
    SigningSecret      = "per-tenant-secret",
    SignatureAlgorithm = WebhookSignatureAlgorithm.HmacSha512
});
```

## Batch delivery

The channel implements `IBatchEventPublishChannel<WebhookPublishOptions>` for dispatching multiple events in a single HTTP call:

```csharp
var batchChannel = serviceProvider.GetRequiredService<IBatchEventPublishChannel<WebhookPublishOptions>>();

await batchChannel.PublishBatchAsync(events, new WebhookPublishOptions
{
    EndpointUrl = "https://partner.example.com/events/batch"
});
```

## Custom serialiser

Register a custom `IEventSerializer` to support additional content types:

```csharp
builder.Services
    .AddEventPublisher()
    .UseWebhook(options => options.MessageFormat = "application/x-protobuf")
    .UseWebhookMessageSerializer<ProtobufEventSerializer>();
```

```csharp
public class ProtobufEventSerializer : IEventSerializer
{
    public string Format => "application/x-protobuf";

    public async Task<byte[]> SerializeAsync(CloudEvent @event, CancellationToken cancellationToken = default)
    {
        // ... protobuf serialisation
    }
}
```

## Custom signature provider

```csharp
builder.Services
    .AddEventPublisher()
    .UseWebhook(options => { /* ... */ })
    .UseWebhookSignatureProvider<Ed25519SignatureProvider>();
```

## Related pages

- [Publisher Channels Overview](README.md)
- [Event Publisher](../concepts/event-publisher.md)

