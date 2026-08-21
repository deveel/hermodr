# HTTP Channel

The `Hermodr.Publisher.Http` package is a lightweight publish channel that delivers CloudEvents directly to **statically-configured HTTP endpoints** using the [CloudEvents HTTP binding](https://github.com/cloudevents/spec/blob/main/cloudevents/bindings/http-protocol-binding.md) (structured or binary content mode). A single publish call fans the event out to every configured endpoint concurrently, each with its own authentication, resilience, and wire format.

Unlike the [Webhook publisher](webhook.md), the HTTP channel has **no subscriber registry, no HMAC signing, and no delivery receipts** — it is the right-sized, trust-based complement for delivering events to known service endpoints such as an internal API gateway, a Function-app trigger URL, an edge service, or a CloudEvents-native platform (Azure Event Grid custom topics, Knative).

## Installation

```bash
dotnet add package Hermodr.Publisher.Http
```

## Registration

### Inline configuration

```csharp
using Hermodr;

builder.Services
    .AddEventPublisher()
    .AddHttpEventPublisherChannel(options =>
    {
        options.Endpoints = new[]
        {
            new HttpEndpoint
            {
                Url = "https://api.example.com/events",
                Format = EventMessageFormat.CloudEventsJson,
            },
            new HttpEndpoint
            {
                Url = "https://edge.example.com/hooks/events",
                Format = EventMessageFormat.CloudEventsBinary,
                Authentication = new HttpEndpointAuthentication
                {
                    AuthenticationMode = HttpAuthenticationMode.Bearer,
                    BearerToken = "s3cr3t-token",
                },
            },
        };
    });
```

### From `appsettings.json`

```csharp
builder.Services
    .AddEventPublisher()
    .AddHttpEventPublisherChannel("Events:Http");
```

```json
// appsettings.json
{
  "Events": {
    "Http": {
      "Endpoints": [
        {
          "Url": "https://api.example.com/events",
          "Format": "cloudevents+json"
        },
        {
          "Url": "https://edge.example.com/hooks/events",
          "Format": "cloudevents+binary",
          "Authentication": {
            "AuthenticationMode": "Bearer",
            "BearerToken": "s3cr3t-token"
          }
        }
      ]
    }
  }
}
```

When the host has registered an `IConfiguration` instance (as host builders do), the section is bound eagerly so each endpoint's named client can be wired at setup time. Otherwise the options are bound lazily and only the default client is pre-registered; attach per-endpoint authentication via `ConfigureHttpClient`.

## Options reference

### `HttpPublishOptions`

| Property | Description |
|----------|-------------|
| `Endpoints` | The statically-configured endpoints events are delivered to. Must contain at least one endpoint, each with an absolute URL. |

### `HttpEndpoint`

| Property | Description |
|----------|-------------|
| `Url` | The absolute URL of the endpoint the event is delivered to (required). |
| `Format` | The serialization format of the request body. Defaults to `EventMessageFormat.CloudEventsJson` (`application/cloudevents+json`). Structured modes (`cloudevents+json`, `cloudevents+xml`) embed the whole CloudEvent in the body; binary mode (`cloudevents+binary`) sends the raw `data` as the body and the attributes as `ce-*` headers. Custom serializer format strings are also accepted. |
| `HttpClientName` | Optional name of the named `HttpClient` resolved through `IHttpClientFactory` for this endpoint. When unset, a unique per-endpoint client is allocated (or the default `Hermodr.Http` client is reused). |
| `Authentication` | The `HttpEndpointAuthentication` applied to requests for this endpoint. When `null`, requests are sent unauthenticated. |
| `Resilience` | The `HttpResilienceOptions` for this endpoint's named client. When `null`, the standard resilience defaults are used. |
| `RequestTimeout` | Timeout applied to each individual HTTP request. Defaults to 30 seconds. |
| `AdditionalHeaders` | Extra HTTP headers merged into every request for this endpoint. |

## Message formats

The endpoint `Format` selects the wire format using the well-known `EventMessageFormat` identifiers (or a custom `IEventSerializer` format string):

- **Structured content mode** — `cloudevents+json` (default) and `cloudevents+xml` embed the entire CloudEvent envelope (all attributes plus `data`) in the request body with the canonical `application/cloudevents+json` / `application/cloudevents+xml` `Content-Type`.
- **Binary content mode** — `cloudevents+binary` sends the raw event `data` as the request body (with its native `datacontenttype` as `Content-Type`, falling back to `application/json`) and projects every CloudEvent context attribute — including extensions — onto `ce-*` HTTP headers per the [CloudEvents HTTP binding §3.1](https://github.com/cloudevents/spec/blob/main/cloudevents/bindings/http-protocol-binding.md#31-binary-content-mode). This allows the event `data` to be any content type (e.g. `application/protobuf`) and attribute-based routing at the HTTP layer.

The `ce-*` headers emitted in binary mode are applied first so your `AdditionalHeaders` can still override them on key collision.

## Authentication

Per-endpoint authentication is applied to the endpoint's named `HttpClient` via a built-in `DelegatingHandler` registered at DI setup time — no per-request logic is required:

| `HttpAuthenticationMode` | Behavior |
|--------------------------|----------|
| `None` | Requests are sent anonymously (default). |
| `Bearer` | Sends the configured token in the `Authorization: Bearer <token>` header. |
| `ApiKey` | Sends the configured key in a configurable header (defaults to `X-Api-Key`). |

For any scheme not covered (OAuth2 flows, mTLS, custom signing, ...) attach a custom `DelegatingHandler` to the endpoint's named client:

```csharp
builder.Services
    .AddEventPublisher()
    .AddHttpEventPublisherChannel(options =>
    {
        options.Endpoints = new[]
        {
            new HttpEndpoint
            {
                Url = "https://api.example.com/events",
                HttpClientName = "Events.Api",
            },
        };
    })
    .ConfigureHttpClient("Events.Api", client =>
    {
        client.AddHttpMessageHandler<MyCustomAuthHandler>();
        client.BaseAddress = new Uri("https://api.example.com");
        client.Timeout = TimeSpan.FromSeconds(10);
    });
```

## Resilience

Every endpoint's named client is configured through `AddStandardResilienceHandler()`, which builds the standard pipeline (retry, circuit-breaker, total-request timeout, optional rate limiter). `HttpResilienceOptions` exposes the bindable subset:

| Property | Description |
|----------|-------------|
| `RetryMaxAttempts` | Maximum number of retries after the initial attempt (default 3). |
| `RetryDelay` | Base delay between attempts, grown exponentially (default from the library). |
| `RetryUseJitter` | Whether retry delays are jittered (default `true`). |
| `CircuitBreakerSamplingDuration` | Window over which failures are sampled for the circuit breaker. |
| `CircuitBreakerMinimumThroughput` | Minimum requests required before the breaker evaluates the failure ratio. |
| `CircuitBreakerFailureRatio` | Failure ratio (0.0–1.0) that trips the breaker. |
| `CircuitBreakerBreakDuration` | How long the breaker stays open before allowing requests through. |

By default the status codes `429`, `500`, `502`, `503`, and `504` are treated as transient and retried. Properties left `null` keep the library-provided defaults. Settings not exposed here (such as the rate limiter) can be configured directly through the `IHttpStandardResiliencePipelineBuilder` surfaced by `ConfigureHttpClient`.

## Error handling

Transport failures and non-success status codes surface as `HttpPublishException` subclasses, so the existing error-handling pipeline (including the dead-letter channel) engages automatically:

- **`HttpTransportException`** — a network-level failure (connection refused, DNS, timeout after retries) when delivering to an endpoint.
- **`HttpStatusCodeException`** — the endpoint returned a non-success status code after all retry attempts; carries the `StatusCode`.

Because one publish call fans out to every endpoint, a multi-endpoint failure rethrows as `HttpPublishException` with a `Failures` collection listing each individual endpoint failure. All endpoints are attempted regardless of individual failures (no short-circuiting).

## Observability

Each endpoint delivery runs under an OpenTelemetry publish span (`transport.publish.http`, `ActivityKind.Producer`) tagged with the event `type`, `messaging.system = "http"`, the destination URL, and the request method (`POST`). Failures mark the span with an error status, consistent with the other transport channels.

## Channel metadata

`HttpPublishOptions` implements `IChannelMetadataSource`, exposing the endpoints through `EventTransports.Http` as `url` (and `url.N` for subsequent endpoints). The AsyncAPI / OpenAPI exporters pick this up for channel bindings.

## Related pages

- [Publisher Channels Overview](README.md)
- [Webhook Channel](webhook.md) — dynamic subscriber management, HMAC signing, and delivery receipts
- [Typed Channels](typed-channels.md)
- [Named Channels](named-channels.md)
- [Publish Channels](../concepts/publish-channels.md)
- [Publish Error Handling](error-handling.md)