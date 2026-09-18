# OrderService — gRPC Publisher

**Location:** [`samples/grpc-publisher/`](https://github.com/deveel/hermodr/tree/main/samples/grpc-publisher)  
**Transport:** gRPC (`Hermodr.Publisher.Grpc`)  
**Pattern:** Channel configured from `appsettings.json`, pluggable `IGrpcEventSender` over a shared `.proto` contract, unary and client-streaming delivery

## What it demonstrates

- a two-project setup: an ASP.NET Core **gRPC receiver** and a **publisher** console application
- a single shared `protos/order_events.proto` generated on both sides (server + client)
- `AddGrpcEventPublisherChannel("Events:Grpc")` bound from configuration (address, headers)
- `AddGrpcEventSender<T>()` wrapping the `.proto`-generated client, forwarding
  `GrpcCallContext` values (headers, deadline, cancellation token) to the RPC call
- **unary RPC** delivery for single events (`publisher.PublishAsync(...)`)
- **client-streaming RPC** delivery for batches (`IBatchEventPublishChannel.PublishBatchAsync(...)`)
- a gRPC server endpoint over HTTP/2 with the ASP.NET Core development certificate

## Flow

```
OrderCreated / OrderConfirmed / OrderShipped
    │
    ▼
EventPublisher ──► gRPC channel ──► IGrpcEventSender
                                          │
                              OrderEvents.OrderEventsClient (.proto-generated)
                                          │
                              ▼ unary: PublishEvent      ▼ batch: PublishEventBatch
                              OrderService.GrpcServer (ASP.NET Core)
```

## Run it

```bash
# One-time: trust the ASP.NET Core development certificate
dotnet dev-certs https --trust

# Terminal 1 — the gRPC receiver (listens on https://localhost:5095)
cd samples/grpc-publisher/OrderService.GrpcServer
dotnet run

# Terminal 2 — the publisher
cd samples/grpc-publisher/OrderService.Publisher
dotnet run
```

For the full walkthrough, see the [sample README](https://github.com/deveel/hermodr/tree/main/samples/grpc-publisher/README.md).
