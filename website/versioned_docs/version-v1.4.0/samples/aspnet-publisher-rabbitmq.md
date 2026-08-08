# Sample: OrderService — Minimal API + RabbitMQ

**Location:** [`samples/aspnet-publisher/OrderService/`](https://github.com/deveel/hermodr/tree/main/samples/aspnet-publisher/OrderService)  
**Framework:** ASP.NET Core 9 Minimal API  
**Transport:** RabbitMQ — `Hermodr.Publisher.RabbitMq`

---

## Overview

This sample shows how to embed **Hermodr** into a real-world microservice.  
The service manages the lifecycle of an **Order** entity and publishes a CloudEvent-compliant domain event to a RabbitMQ exchange at every state transition.

```
POST   /orders              → OrderCreated    → exchange: orders  routing-key: order.created
PUT    /orders/{id}/confirm → OrderConfirmed  → exchange: orders  routing-key: order.confirmed
PUT    /orders/{id}/ship    → OrderShipped    → exchange: orders  routing-key: order.shipped
PUT    /orders/{id}/deliver → OrderDelivered  → exchange: orders  routing-key: order.delivered
PUT    /orders/{id}/cancel  → OrderCancelled  → exchange: orders  routing-key: order.cancelled
```

---

## What this sample demonstrates

### 1. Annotated event data classes

Each domain event is a plain C# class decorated with framework attributes:

```csharp
[Event("order.created", "1.0")]
[AmqpExchange("orders", ExchangeType = "topic")]
[AmqpRoutingKey("order.created")]
public class OrderCreated
{
    public Guid OrderId { get; set; }
    public string CustomerId { get; set; } = "";
    public decimal TotalAmount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
```

- `[Event]` — registers the event type and version for the CloudEvent `type` attribute.
- `[AmqpExchange]` — declares the target exchange. The exchange is created by the channel if it does not already exist.
- `[AmqpRoutingKey]` — sets the AMQP routing key used when publishing to topic or direct exchanges.

> See `Events/*.cs` in the sample for all five events.

### 2. DI registration (`Program.cs`)

```csharp
builder.Services.AddEventPublisher(pub =>
{
    pub.Source = new Uri("https://orders.example.com");
})
.AddRabbitMq(builder.Configuration.GetSection("Events:RabbitMq"))
.AddRabbitMq<OrderCreated>()
.AddRabbitMq<OrderConfirmed>()
.AddRabbitMq<OrderShipped>()
.AddRabbitMq<OrderDelivered>()
.AddRabbitMq<OrderCancelled>();
```

- `AddEventPublisher()` sets up the core `IEventPublisher` service.
- The shared `AddRabbitMq(section)` call binds the connection string and shared options from `appsettings.json`.
- Each `AddRabbitMq<TEvent>()` call creates a **typed channel** whose exchange, routing key, and content type are read directly from the annotations on that event class — no code duplication.

### 3. Publishing inside a service

```csharp
public class OrderManagementService : IOrderService
{
    private readonly IEventPublisher _publisher;

    public OrderManagementService(IEventPublisher publisher) =>
        _publisher = publisher;

    public async Task<Order> CreateAsync(CreateOrderRequest request, CancellationToken ct)
    {
        var order = Order.Create(request);
        // ... persist order ...

        await _publisher.PublishAsync(new OrderCreated
        {
            OrderId    = order.Id,
            CustomerId = order.CustomerId,
            TotalAmount = order.TotalAmount,
            CreatedAt  = order.CreatedAt
        }, cancellationToken: ct);

        return order;
    }
    // ... ConfirmAsync, ShipAsync, DeliverAsync, CancelAsync ...
}
```

`IEventPublisher` is injected as a normal dependency. Calling `PublishAsync<TEvent>()` wraps the payload in a CloudEvent envelope and dispatches it through the matching typed channel.

---

## Prerequisites

| Tool | Version |
|------|---------|
| [.NET SDK](https://dotnet.microsoft.com/download) | 9.0 + |
| [Docker](https://www.docker.com/) | any recent version |

---

## Running the sample

```bash
cd samples/aspnet-publisher/OrderService

# Start RabbitMQ (management UI on http://localhost:15672, guest/guest)
docker compose up -d

# Run the API
dotnet run
```

The API listens on `http://localhost:5000` (and `https://localhost:5001`).

### Exercise the full lifecycle

```bash
# 1. Create an order
ORDER_ID=$(curl -s -X POST http://localhost:5000/orders \
  -H "Content-Type: application/json" \
  -d '{
    "customerId": "cust-42",
    "items": [
      { "productId": "prod-1", "productName": "Widget", "quantity": 2, "unitPrice": 9.99 }
    ]
  }' | jq -r '.id')

echo "Created order: $ORDER_ID"

# 2. Confirm
curl -s -X PUT "http://localhost:5000/orders/$ORDER_ID/confirm" | jq

# 3. Ship
curl -s -X PUT "http://localhost:5000/orders/$ORDER_ID/ship" \
  -H "Content-Type: application/json" \
  -d '{ "trackingNumber": "1Z999AA10123456784", "carrier": "UPS" }' | jq

# 4. Deliver
curl -s -X PUT "http://localhost:5000/orders/$ORDER_ID/deliver" | jq
```

After each step, open the [RabbitMQ management UI](http://localhost:15672) and inspect the `orders` exchange to verify the messages arrived.

---

## Configuration reference

| Key (`Events:RabbitMq:*`) | Default | Description |
|---------------------------|---------|-------------|
| `ConnectionString` | `amqp://guest:guest@localhost:5672` | RabbitMQ AMQP URI |
| `ExchangeName` | `orders` | Fallback exchange (overridden per-event by `[AmqpExchange]`) |
| `PersistentMessages` | `true` | Survive broker restarts |
| `PublisherConfirms` | `true` | Wait for broker `ACK` |
| `ConfirmTimeout` | `00:00:05` | Timeout for broker `ACK` |
| `ClientName` | `order-service` | Connection label shown in management UI |

Override any value with an environment variable using the standard ASP.NET Core convention:

```bash
Events__RabbitMq__ConnectionString=amqp://user:pass@rabbitmq:5672 dotnet run
```

---

## Related documentation

- [RabbitMQ Channel](../publishers/rabbitmq.md)
- [Typed Channels](../publishers/typed-channels.md)
- [Event Annotations](../concepts/event-annotations.md)
- [Event Publisher](../concepts/event-publisher.md)

