# OrderService — gRPC Publisher

**Path:** `grpc-publisher/`
**Framework:** .NET 9 (console publisher + ASP.NET Core gRPC receiver)
**Transport:** gRPC (`Hermodr.Publisher.Grpc`)

A two-project sample demonstrating the **gRPC publish channel**: an Order publisher that delivers CloudEvents to a gRPC receiver implementing an application-defined Protobuf contract.

- **Single events** are delivered through a **unary RPC** (`PublishEvent`).
- **Batches** are delivered through a **client-streaming RPC** (`PublishEventBatch`).
- The wire contract is owned by the application (`protos/order_events.proto`): the channel is transport-agnostic and delegates the RPC invocation to a pluggable `IGrpcEventSender` that wraps the `.proto`-generated client.

## Projects

| Project | Role |
|---------|------|
| `OrderService.GrpcServer` | ASP.NET Core gRPC receiver: implements the `OrderEvents` service generated from the shared `.proto`, logging every received event (and its call headers) |
| `OrderService.Publisher` | Console publisher: registers the Hermodr gRPC channel from `appsettings.json` and publishes Order lifecycle events |

## Key Hermodr patterns shown

| Pattern | Where |
|---------|-------|
| `[Event]`-annotated event records | `OrderService.Publisher/Events/*.cs` |
| `AddGrpcEventPublisherChannel("Events:Grpc")` bound from configuration | `OrderService.Publisher/Program.cs`, `appsettings.json` |
| `AddGrpcEventSender<T>()` registration of a `.proto`-generated sender | `OrderService.Publisher/Program.cs` |
| `IGrpcEventSender` implementation forwarding `GrpcCallContext` (headers, deadline, cancellation) to the generated client | `OrderService.Publisher/GrpcSenders/OrderEventsGrpcSender.cs` |
| Batch publishing via `IBatchEventPublishChannel` (client-streaming) | `OrderService.Publisher/Program.cs` |
| Shared `.proto` contract generated on both sides (server + client) | `protos/order_events.proto`, `<Protobuf>` items in both `.csproj` files |
| gRPC server endpoint (HTTP/2 + dev certificate) | `OrderService.GrpcServer/Program.cs` |

## Quick start

```bash
# 1. Trust the ASP.NET Core development certificate (one-time setup)
dotnet dev-certs https --trust

# 2. Start the gRPC receiver (terminal 1)
cd OrderService.GrpcServer
dotnet run
# The receiver listens on https://localhost:5095 (HTTP/2)

# 3. Run the publisher (terminal 2)
cd OrderService.Publisher
dotnet run
```

Expected output in the publisher console:

```
=== Hermodr gRPC Publisher Sample ===

Publishing lifecycle events for order 'a1b2c3...' (unary RPC)...

All single events delivered.

Publishing a batch of events (client-streaming RPC)...

Batch delivered.

=== Sample completed: check the receiver console for the received events ===
```

The receiver console shows every event received through the unary and client-streaming RPCs, including the custom `x-sample-client` header configured on the endpoint.

## Configuration

The publisher channel is configured from `OrderService.Publisher/appsettings.json`:

```json
{
  "Events": {
    "Grpc": {
      "Endpoints": [
        {
          "Address": "https://localhost:5095",
          "Headers": {
            "x-sample-client": "order-publisher"
          }
        }
      ]
    }
  }
}
```

Each endpoint entry supports `Address`, `HttpClientName`, `SenderName`, `Deadline`, and `Headers` — see the [gRPC Channel documentation](https://hermodr.deveel.com/docs/publishers/grpc) for the full configuration reference, including TLS, mTLS, and `CallCredentials` through the standard `GrpcChannel` tooling.
