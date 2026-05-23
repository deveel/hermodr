# Hermodr — Samples

This directory contains runnable sample projects that demonstrate the major use cases of the **Hermodr** framework.  
Each sample is self-contained: it has its own project file, configuration, and (where needed) a `docker-compose.yml` to spin up the required infrastructure.

---

## Available samples

| Sample | Transport | Description |
|--------|-----------|-------------|
| [aspnet-publisher/OrderService.SimplePublisher](aspnet-publisher/OrderService.SimplePublisher/README.md) | RabbitMQ | ASP.NET Core Minimal API microservice that publishes Order lifecycle events to a RabbitMQ exchange |
| [outbox-inapp/OrderService.InAppOutbox](outbox-inapp/OrderService.InAppOutbox/README.md) | RabbitMQ | Single-process transactional outbox with EF Core SQLite storage and an in-process relay |
| [outbox-inapp/OrderService.InAppOutboxScheduling](outbox-inapp/OrderService.InAppOutboxScheduling/README.md) | RabbitMQ | In-process transactional outbox that records events immediately and delays only broker delivery until a scheduled UTC time |
| [outbox-relay](outbox-relay/README.md) | MassTransit / RabbitMQ | Split transactional outbox with a separate relay worker consuming the shared SQLite outbox |
| [deadletter-inproc/OrderService.InProcDeadLetter](deadletter-inproc/OrderService.InProcDeadLetter/README.md) | In-memory sample channels | Immediate in-process dead-letter interception and replay through the same publisher pipeline |
| [deadletter-relay](deadletter-relay/README.md) | EF Core SQLite + in-memory recovery channel | Split dead-letter sample with a publisher app and a background worker sharing the replay repository |
| [opentelemetry/OrderService.OpenTelemetry](opentelemetry/OrderService.OpenTelemetry/README.md) | In-memory sample channels | OpenTelemetry instrumentation with producer/consumer spans, trace context injection/extraction, and metrics |

---

## aspnet-publisher / OrderService.SimplePublisher

**Path:** `aspnet-publisher/OrderService.SimplePublisher/`  
**Framework:** ASP.NET Core 9 Minimal API  
**Transport:** RabbitMQ (`Hermodr.Publisher.RabbitMq`)

A microservice managing the full lifecycle of an **Order** entity.  
Every state transition — creation, confirmation, shipping, delivery, cancellation — publishes a typed, CloudEvent-compliant domain event to a RabbitMQ exchange.

### Key Hermodr patterns shown

| Pattern | Where |
|---------|-------|
| `[Event]`, `[AmqpExchange]`, `[AmqpRoutingKey]` on event data classes | `Events/*.cs` |
| `AddEventPublisher()` with publisher `Source` | `Program.cs` |
| Per-event typed RabbitMQ channels via `AddRabbitMq<TEvent>()` | `Program.cs` |
| `IEventPublisher.PublishAsync<TEvent>()` inside an application service | `Services/OrderManagementService.cs` |
| Minimal API endpoints that drive state transitions | `Endpoints/OrderEndpoints.cs` |

### Quick start

```bash
cd aspnet-publisher/OrderService

# 1. Start RabbitMQ
docker compose up -d

# 2. Run the API
dotnet run

# 3. Create an order
curl -s -X POST http://localhost:5000/orders \
  -H "Content-Type: application/json" \
  -d '{
    "customerId": "cust-42",
    "items": [{ "productId": "prod-1", "productName": "Widget", "quantity": 2, "unitPrice": 9.99 }]
  }' | jq
```

See the [sample README](aspnet-publisher/OrderService.SimplePublisher/README.md) for the full walkthrough.

---

## deadletter-inproc / OrderService.InProcDeadLetter

**Path:** `deadletter-inproc/OrderService.InProcDeadLetter/`  
**Framework:** .NET 9 console app  
**Transport:** Sample in-memory channels

This sample shows the lightest dead-letter integration: one channel fails, an `AddDeadLetter(...).UseHandler(...)` callback captures the failed `CloudEvent`, and the same process immediately replays it through the publisher pipeline to a recovery channel.

Quick start:

```bash
cd deadletter-inproc/OrderService.InProcDeadLetter
dotnet run
```

See the [sample README](deadletter-inproc/OrderService.InProcDeadLetter/README.md) for the full walkthrough.

---

## deadletter-relay

**Path:** `deadletter-relay/`  
**Framework:** .NET 9 console app + worker  
**Transport:** EF Core SQLite dead-letter store with an in-memory recovery channel

This sample splits dead-letter handling across two applications. `OrderService.Publisher` writes failed deliveries into a shared SQLite repository, and `OrderService.DeadLetterWorker` polls that store and replays pending messages in the background.

Quick start:

```bash
cd deadletter-relay/OrderService.DeadLetterWorker
dotnet run

cd ../OrderService.Publisher
dotnet run
```

See the [sample README](deadletter-relay/README.md) for the full walkthrough.

---

## Adding a new sample

1. Create a sub-folder under `samples/` that reflects the pattern being demonstrated (e.g. `azure-servicebus-publisher/`).
2. Add at minimum:
   - A runnable project (`.csproj`)
   - An `appsettings.json` with sensible defaults
   - A `docker-compose.yml` if external infrastructure is required
   - A `README.md` following the same structure as the existing samples
3. Register the new sample in the table above and in [`docs/samples/README.md`](../docs/samples/README.md).
