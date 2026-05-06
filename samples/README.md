# Deveel Events — Samples

This directory contains runnable sample projects that demonstrate the major use cases of the **Deveel Events** framework.  
Each sample is self-contained: it has its own project file, configuration, and (where needed) a `docker-compose.yml` to spin up the required infrastructure.

---

## Available samples

| Sample | Transport | Description |
|--------|-----------|-------------|
| [aspnet-publisher/OrderService](aspnet-publisher/OrderService/README.md) | RabbitMQ | ASP.NET Core Minimal API microservice that publishes Order lifecycle events to a RabbitMQ exchange |

---

## aspnet-publisher / OrderService

**Path:** `aspnet-publisher/OrderService/`  
**Framework:** ASP.NET Core 9 Minimal API  
**Transport:** RabbitMQ (`Deveel.Events.Publisher.RabbitMq`)

A microservice managing the full lifecycle of an **Order** entity.  
Every state transition — creation, confirmation, shipping, delivery, cancellation — publishes a typed, CloudEvent-compliant domain event to a RabbitMQ exchange.

### Key Deveel.Events patterns shown

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

See the [sample README](aspnet-publisher/OrderService/README.md) for the full walkthrough.

---

## Adding a new sample

1. Create a sub-folder under `samples/` that reflects the pattern being demonstrated (e.g. `azure-servicebus-publisher/`).
2. Add at minimum:
   - A runnable project (`.csproj`)
   - An `appsettings.json` with sensible defaults
   - A `docker-compose.yml` if external infrastructure is required
   - A `README.md` following the same structure as the existing samples
3. Register the new sample in the table above and in [`docs/samples/README.md`](../docs/samples/README.md).

