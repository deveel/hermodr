# OrderService — Minimal API sample

A minimal ASP.NET Core microservice that manages the full lifecycle of **Order** entities and publishes a domain event to a **RabbitMQ** exchange at each state transition using the `Deveel.Events` framework.

## What this sample demonstrates

| Concept | Where to look |
|---|---|
| Annotating event data classes with `[Event]`, `[AmqpExchange]`, `[AmqpRoutingKey]` | `Events/*.cs` |
| Registering the event publisher with `AddEventPublisher()` | `Program.cs` |
| Wiring per-event typed RabbitMQ channels with `AddRabbitMq<TEvent>()` | `Program.cs` |
| Injecting and using `IEventPublisher` in a service | `Services/OrderManagementService.cs` |
| Mapping Minimal API endpoints | `Endpoints/OrderEndpoints.cs` |

## Order lifecycle & events

```
POST /orders           → OrderCreated   (order.created)
PUT  /orders/{id}/confirm → OrderConfirmed (order.confirmed)
PUT  /orders/{id}/ship    → OrderShipped   (order.shipped)
PUT  /orders/{id}/deliver → OrderDelivered (order.delivered)
PUT  /orders/{id}/cancel  → OrderCancelled (order.cancelled)
```

Each event is published as a **CloudEvent** envelope to the `orders` RabbitMQ exchange with a per-event routing key declared directly on the event class via `[AmqpRoutingKey]`.

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- [Docker](https://www.docker.com/) (for RabbitMQ)

## Running the sample

### 1. Start RabbitMQ

```bash
docker compose up -d
```

The RabbitMQ management UI will be available at <http://localhost:15672> (guest / guest).

### 2. Run the API

```bash
dotnet run
```

The API listens on `https://localhost:5001` and `http://localhost:5000` by default.  
OpenAPI docs are served at `/openapi/v1.json` in development mode.

### 3. Exercise the lifecycle

```bash
# Create an order
ORDER_ID=$(curl -s -X POST http://localhost:5000/orders \
  -H "Content-Type: application/json" \
  -d '{
    "customerId": "cust-42",
    "items": [
      { "productId": "prod-1", "productName": "Widget", "quantity": 2, "unitPrice": 9.99 }
    ]
  }' | jq -r '.id')

echo "Created order: $ORDER_ID"

# Confirm
curl -s -X PUT "http://localhost:5000/orders/$ORDER_ID/confirm" | jq

# Ship
curl -s -X PUT "http://localhost:5000/orders/$ORDER_ID/ship" \
  -H "Content-Type: application/json" \
  -d '{ "trackingNumber": "1Z999AA10123456784", "carrier": "UPS" }' | jq

# Deliver
curl -s -X PUT "http://localhost:5000/orders/$ORDER_ID/deliver" | jq
```

After each step, check the RabbitMQ management UI to verify that the corresponding message arrived in the `orders` exchange.

## Project structure

```
OrderService/
├── Domain/
│   ├── Order.cs               # Aggregate root + OrderItem + OrderStatus
│   └── OrderContracts.cs      # Request / response DTOs
├── Events/
│   ├── OrderCreated.cs        # [Event] + [AmqpExchange] + [AmqpRoutingKey]
│   ├── OrderConfirmed.cs
│   ├── OrderShipped.cs
│   ├── OrderDelivered.cs
│   └── OrderCancelled.cs
├── Services/
│   ├── IOrderService.cs
│   └── OrderManagementService.cs  # Uses IEventPublisher
├── Endpoints/
│   └── OrderEndpoints.cs      # Minimal API route handlers
├── Program.cs                 # DI + Deveel.Events wiring
├── appsettings.json
├── appsettings.Development.json
└── docker-compose.yml
```

## Configuration reference

`appsettings.json → Events:RabbitMq`

| Key | Default | Description |
|-----|---------|-------------|
| `ConnectionString` | `amqp://guest:guest@localhost:5672` | RabbitMQ AMQP URI |
| `ExchangeName` | `orders` | Fallback exchange (overridden per-event by `[AmqpExchange]`) |
| `PersistentMessages` | `true` | Survive broker restarts |
| `PublisherConfirms` | `true` | Wait for broker ACK |
| `ConfirmTimeout` | `00:00:05` | Timeout for broker ACK |
| `ClientName` | `order-service` | Name shown in management UI |

Override any setting via an environment variable using the standard ASP.NET Core convention:

```bash
Events__RabbitMq__ConnectionString=amqp://user:pass@rabbitmq:5672 dotnet run
```

