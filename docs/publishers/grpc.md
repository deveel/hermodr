# gRPC Channel

The `Hermodr.Publisher.Grpc` package is a publish channel that delivers CloudEvents to **statically-configured gRPC endpoints** using [grpc-dotnet](https://github.com/grpc/grpc-dotnet) (`Grpc.Net.Client`) for the low-level HTTP/2 client. A single publish call fans the event out to every configured endpoint concurrently, each with its own `GrpcChannel`, TLS/mTLS configuration, and event sender.

Unlike the [HTTP channel](http.md), the gRPC channel delegates the actual RPC invocation to a pluggable **`IGrpcEventSender`** strategy that calls your `.proto`-generated gRPC client — keeping the channel transport-agnostic while leveraging the full Hermodr pipeline (enrichment, schema validation, middleware, dead-letter, tracing, delivery log).

> **Note:** The CloudEvents specification defines a [Protobuf *event format*](https://github.com/cloudevents/spec/blob/main/cloudevents/formats/protobuf-format.md) (the `io.cloudevents.v1.CloudEvent` message), but does **not** define a gRPC *service* binding. The gRPC service contract (the `rpc` methods) is therefore defined by your application's `.proto` file, and the channel integrates with it through the `IGrpcEventSender` interface. A `.proto` generator for `[Event]`-annotated types is planned for v1.6.0 (roadmap items 20–22), which will produce both the `.proto` contract and a generated `IGrpcEventSender` automatically.

## Installation

```bash
dotnet add package Hermodr.Publisher.Grpc
```

## Registration

### Inline configuration

```csharp
using Hermodr;

builder.Services
    .AddEventPublisher()
    .AddGrpcEventPublisherChannel(options =>
    {
        options.Endpoints = new[]
        {
            new GrpcEndpoint { Address = "https://svc.example.com:5001" },
            new GrpcEndpoint
            {
                Address = "https://edge.example.com:5001",
                HttpClientName = "Events.Edge",
            },
        };
    })
    .AddGrpcEventSender<MyCloudEventSender>();
```

### From `appsettings.json`

```csharp
builder.Services
    .AddEventPublisher()
    .AddGrpcEventPublisherChannel("Events:Grpc")
    .AddGrpcEventSender<MyCloudEventSender>();
```

```json
// appsettings.json
{
  "Events": {
    "Grpc": {
      "Endpoints": [
        { "Address": "https://svc.example.com:5001" },
        {
          "Address": "https://edge.example.com:5001",
          "HttpClientName": "Events.Edge"
        }
      ]
    }
  }
}
```

## The `IGrpcEventSender` integration seam

The channel owns all transport concerns (fan-out, `GrpcChannel` lifetime, TLS/mTLS, error mapping, telemetry, middleware/dead-letter integration). The actual gRPC RPC is delegated to an `IGrpcEventSender` that you register via `AddGrpcEventSender<T>()`.

A sender receives a `GrpcCallContext` carrying a `CallInvoker` (created from the per-endpoint `GrpcChannel`), the per-endpoint call headers, the deadline, and the cancellation token. A typical sender constructs its generated gRPC client from the `CallInvoker` and forwards the context values to the RPC call:

```csharp
using CloudNative.CloudEvents;
using Grpc.Core;

public sealed class MyCloudEventSender : IGrpcEventSender
{
    public async Task SendAsync(CloudEvent @event, GrpcCallContext context)
    {
        // Construct your .proto-generated gRPC client from the CallInvoker.
        var client = new OrderPub.OrderPubClient(context.CallInvoker);

        // Map the CloudEvent to your protobuf request message.
        var request = new PublishRequest
        {
            Id = @event.Id,
            Type = @event.Type,
            Source = @event.Source?.ToString(),
            Data = CloudEventToJson(@event),
        };

        // Forward the per-call headers, deadline, and cancellation token.
        await client.PublishAsync(
            request,
            headers: context.Headers,
            deadline: context.Deadline,
            cancellationToken: context.CancellationToken);
    }

    public async Task SendBatchAsync(IReadOnlyList<CloudEvent> events, GrpcCallContext context)
    {
        var client = new OrderPub.OrderPubClient(context.CallInvoker);

        // Client-streaming: open the stream and write each event.
        using var call = client.PublishBatch(
            headers: context.Headers,
            deadline: context.Deadline,
            cancellationToken: context.CancellationToken);

        foreach (var @event in events)
        {
            await call.RequestStream.WriteAsync(ToPublishRequest(@event));
        }

        await call.RequestStream.CompleteAsync();
        await call.ResponseAsync;
    }
}
```

### Per-endpoint sender selection

When different endpoints target different gRPC services, assign each endpoint a `SenderName` and register a named sender for each:

```csharp
builder.Services
    .AddEventPublisher()
    .AddGrpcEventPublisherChannel(options =>
    {
        options.Endpoints = new[]
        {
            new GrpcEndpoint
            {
                Address = "https://orders.example.com:5001",
                SenderName = "orders",
            },
            new GrpcEndpoint
            {
                Address = "https://notifications.example.com:5001",
                SenderName = "notifications",
            },
        };
    })
    .AddGrpcEventSender<OrderServiceSender>("orders")
    .AddGrpcEventSender<NotificationServiceSender>("notifications");
```

> **Fail fast:** if an endpoint references a `SenderName` that is not registered in the DI container, publishing fails with a `GrpcPublishException` naming the missing sender instead of silently falling back to the default sender. This prevents events from being serialized and delivered by the wrong sender (and therefore to the wrong gRPC service) due to a typo.

## Options reference

### `GrpcPublishOptions`

| Property | Description |
|----------|-------------|
| `Endpoints` | The statically-configured gRPC endpoints events are delivered to. Must contain at least one endpoint, each with an absolute address. |

### `GrpcEndpoint`

| Property | Description |
|----------|-------------|
| `Address` | The absolute address of the gRPC endpoint (for example `https://svc.example.com:5001`). Must use the `http` or `https` scheme; any other scheme is rejected by validation. Required. |
| `HttpClientName` | Optional name of the named `HttpClient` resolved through `IHttpClientFactory` for this endpoint. When unset, the default `Hermodr.Grpc` client is used. The `HttpClient` is wrapped as a `GrpcChannel` via `GrpcChannelOptions.HttpClient`. |
| `SenderName` | Optional name used to resolve a named `IGrpcEventSender` for this endpoint. When unset, the default (unnamed) sender is used. Publishing fails fast when a configured name is not registered. |
| `Deadline` | Timeout applied to each individual gRPC call. Defaults to 30 seconds. |
| `Headers` | Additional gRPC call metadata (headers) merged into every call for this endpoint. Keys must be valid gRPC ASCII metadata keys (lowercase letters, digits, underscores, hyphens, dots); `-bin` (binary) keys cannot be carried as string values and are rejected by validation. Invalid entries fail validation before any delivery is attempted. |

## Transport security

- **`https` endpoints** deliver events over TLS, configured through the endpoint's named `HttpClient` (see below).
- **`http` endpoints** deliver events over plaintext HTTP/2 (h2c) — convenient for local development, but event payloads are transmitted unencrypted. The channel logs a **warning** at every delivery to a plaintext endpoint; prefer `https` in production.
- Any other scheme (`ftp://`, `file://`, ...) is rejected by options validation at publish time.

## TLS, mTLS, and CallCredentials

TLS, mTLS, and `CallCredentials` are configured through the standard `IHttpClientFactory` / `SocketsHttpHandler` surface — the same surface that `Grpc.Net.Client` already understands. No bespoke gRPC configuration API is introduced:

```csharp
builder.Services
    .AddEventPublisher()
    .AddGrpcEventPublisherChannel(options =>
    {
        options.Endpoints = new[]
        {
            new GrpcEndpoint
            {
                Address = "https://svc.example.com:5001",
                HttpClientName = "Events.Secure",
            },
        };
    })
    .AddGrpcEventSender<MyCloudEventSender>()
    .ConfigureGrpcChannel("Events.Secure", b =>
    {
        b.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            SslOptions = new SslClientAuthenticationOptions
            {
                ClientCertificates = LoadClientCertificate(),
                RemoteCertificateValidationCallback = ValidateServerCertificate,
            },
        });
    });
```

## Connection & DNS management

`GrpcChannel` instances are cached per (client-name, address) pair and live for the lifetime of the application, so repeated publishes reuse the same underlying HTTP/2 connection pool. Because a cached channel keeps the `HttpClient` (and its handler) captured at creation time, the default `Hermodr.Grpc` client is registered with an **infinite handler lifetime** — `IHttpClientFactory` handler rotation would never take effect.

For long-running processes that must pick up DNS changes, configure the underlying handler's connection lifetime instead:

```csharp
builder.Services
    .AddEventPublisher()
    .AddGrpcEventPublisherChannel(options => { /* ... */ })
    .AddGrpcEventSender<MyCloudEventSender>()
    .ConfigureGrpcChannel("Hermodr.Grpc", b =>
    {
        b.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
        });
    });
```

For full control over `GrpcChannel` creation (retry policies via `ServiceConfig`, message-size limits, keep-alive pings), register a custom `IGrpcChannelFactory`.

## Batch publishing (client-streaming RPC)

The channel implements `IBatchEventPublishChannel`, so you can publish a batch of events via a single client-streaming RPC per endpoint. The channel fans the batch out to all configured endpoints concurrently:

```csharp
var batchChannel = publisher.GetChannels()
    .OfType<IBatchEventPublishChannel>()
    .First();

await batchChannel.PublishBatchAsync(new[]
{
    orderPlaced1,
    orderPlaced2,
    orderPlaced3,
}, cancellationToken: ct);
```

The sender's `SendBatchAsync` method receives the full event list and is responsible for opening the client-streaming RPC and writing each event to the request stream.

## Error handling

Transport failures and non-OK gRPC statuses surface as `GrpcPublishException` subclasses, so the existing error-handling pipeline (including the dead-letter channel) engages automatically:

- **`GrpcTransportException`** — a transport-level failure (connection refused, DNS, timeout, deadline, non-OK gRPC `StatusCode`) when delivering to an endpoint. Carries the gRPC `StatusCode` when available.
- **`GrpcPublishException`** — the base type, used for two purposes: aggregated failure when multiple endpoints fail (the `Failures` collection lists each individual endpoint failure), and non-transport failures such as sender bugs, missing named senders, or invalid configuration, which are not wrapped as transport errors so callers can distinguish them.

Because one publish call fans out to every endpoint, a multi-endpoint failure rethrows as `GrpcPublishException` with a `Failures` collection listing each individual endpoint failure. All endpoints are attempted regardless of individual failures (no short-circuiting). Endpoint faults always take precedence over cancellation, so a cancelled publish never hides a delivery failure; endpoints cancelled mid-flight are reported with a warning log.

## Observability

Each endpoint delivery runs under an OpenTelemetry publish span (`transport.publish.grpc`, `ActivityKind.Producer`) tagged with the event `type`, `messaging.system = "grpc"`, the destination address, and the RPC type (`unary` or `client_streaming`). Failures mark the span with an error status, consistent with the other transport channels.

## Channel metadata

`GrpcPublishOptions` implements `IChannelMetadataSource`, exposing the endpoints through `EventTransports.Grpc` as `address` (and `address.N` for subsequent endpoints). The AsyncAPI / OpenAPI exporters pick this up for channel bindings.

## Related pages

- [Publisher Channels Overview](README.md)
- [HTTP Channel](http.md) — lightweight CloudEvents delivery to known HTTP endpoints
- [Typed Channels](typed-channels.md)
- [Named Channels](named-channels.md)
- [Publish Channels](../concepts/publish-channels.md)
- [Publish Error Handling](error-handling.md)
