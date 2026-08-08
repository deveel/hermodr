# Sample: OrderService — In-Process Outbox + RabbitMQ

**Location:** [`samples/outbox-inapp/OrderService.InAppOutbox/`](https://github.com/deveel/hermodr/tree/main/samples/outbox-inapp/OrderService.InAppOutbox)  
**Framework:** ASP.NET Core 9 Minimal API  
**Transport:** RabbitMQ — `Hermodr.Publisher.RabbitMq`  
**Pattern:** Transactional Outbox (in-process relay)

---

## Overview

This sample shows how to apply the **Transactional Outbox** pattern inside a **single ASP.NET Core process**.  
Order domain events are first persisted to a SQLite outbox table (via EF Core) in the same transaction as the domain write; an in-process `BackgroundService` relay then polls the table and forwards pending CloudEvents to a RabbitMQ exchange.

```
POST   /orders              → OrderCreated    → outbox → RabbitMQ  exchange: orders  key: order.created
PUT    /orders/{id}/confirm → OrderConfirmed  → outbox → RabbitMQ  exchange: orders  key: order.confirmed
PUT    /orders/{id}/ship    → OrderShipped    → outbox → RabbitMQ  exchange: orders  key: order.shipped
PUT    /orders/{id}/deliver → OrderDelivered  → outbox → RabbitMQ  exchange: orders  key: order.delivered
PUT    /orders/{id}/cancel  → OrderCancelled  → outbox → RabbitMQ  exchange: orders  key: order.cancelled
```

### Architecture

```
HTTP request
    │
    ▼
OrderManagementService.PublishAsync(event)
    │
    ▼
OutboxPublishChannel  ──── INSERT into SQLite (outbox.db)
                                  │
              ┌───────────────────┘  (polled every 5 s)
              │
              ▼
OutboxRelayService  (BackgroundService — same process)
    │   reads Pending rows, marks them Sending
    │
    ▼
RabbitMqPublishChannel  ──── AMQP publish to RabbitMQ broker
```

The `OutboxPublishChannel` detects the `OutboxRelayPublishOptions` signal emitted by the relay and short-circuits (skips persistence), so the RabbitMQ channel handles the forwarded event without re-persisting it.

---

## What this sample demonstrates

### 1. Outbox registration without a relay

The `OutboxPublishChannel` is registered via the EF Core integration package:

```csharp
events
    .AddEntityFrameworkOutbox(opts =>
        opts.UseSqlite(connectionString))
    .WithFactory<OrderOutboxMessageFactory>()
    .WithRelay(opts =>
    {
        opts.Interval     = TimeSpan.FromSeconds(5);
        opts.MaxBatchSize = 50;
    });
```

`.WithRelay()` wires up `OutboxRelayService<DbOutboxMessage>` as an `IHostedService` that lives in the **same process** as the API.

### 2. Transport channel registration

The RabbitMQ channels are registered separately — they are only invoked by the relay, not by direct `PublishAsync` calls from business code:

```csharp
events
    .AddRabbitMq("Events:RabbitMq")
    .AddRabbitMq<OrderCreated>("Events:RabbitMq")
    .AddRabbitMq<OrderConfirmed>("Events:RabbitMq")
    .AddRabbitMq<OrderShipped>("Events:RabbitMq")
    .AddRabbitMq<OrderDelivered>("Events:RabbitMq")
    .AddRabbitMq<OrderCancelled>("Events:RabbitMq");
```

### 3. Annotated event classes

Each event carries `[AmqpExchange]` and `[AmqpRoutingKey]` attributes that the RabbitMQ channel reads to route the message:

```csharp
[Event("order.created", "1.0")]
[AmqpExchange("orders")]
[AmqpRoutingKey("order.created")]
public sealed class OrderCreated
{
    public Guid OrderId { get; set; }
    public string CustomerId { get; set; } = default!;
    public IReadOnlyList<OrderCreatedItem> Items { get; set; } = [];
    public decimal TotalAmount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
```

### 4. Thin message factory

The factory creates a `DbOutboxMessage` from the inbound `CloudEvent` using the `PopulateFromCloudEvent` helper:

```csharp
public sealed class OrderOutboxMessageFactory : IOutboxMessageFactory<DbOutboxMessage>
{
    public DbOutboxMessage Create(CloudEvent cloudEvent, OutboxPublishOptions? options = null)
    {
        var message = new DbOutboxMessage();
        message.PopulateFromCloudEvent(cloudEvent);
        return message;
    }
}
```

### 5. Publishing from business code

Business code publishes events as if no outbox exists — the channel is selected transparently by the publisher pipeline:

```csharp
await _publisher.PublishAsync(new OrderCreated
{
    OrderId     = order.Id,
    CustomerId  = order.CustomerId,
    TotalAmount = order.TotalAmount,
    CreatedAt   = order.CreatedAt,
    Items       = /* ... */
}, cancellationToken: ct);
```

---

## Prerequisites

| Tool | Version |
|------|---------|
| [.NET SDK](https://dotnet.microsoft.com/download) | 9.0 + |
| [Docker](https://www.docker.com/) | any recent version |

---

## Running the sample

```bash
cd samples/outbox-inapp/OrderService.InAppOutbox

# Start RabbitMQ (management UI on http://localhost:15672, guest/guest)
docker compose up -d

# Run the API + in-process relay
dotnet run
```

The API listens on `http://localhost:5000`. The SQLite outbox file (`outbox.db`) is created automatically in the working directory.

### Exercise the full lifecycle

```bash
# 1. Create an order
ORDER_ID=$(curl -s -X POST http://localhost:5000/orders \
  -H "Content-Type: application/json" \
  -d '{
    "customerId": "cust-42",
    "items": [
      { "productId": "sku-001", "productName": "Widget", "quantity": 2, "unitPrice": 9.99 }
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

Within 5 seconds of each request, the relay logs a `Sending` update and a `Sent` confirmation. The messages appear in the [RabbitMQ management UI](http://localhost:15672) under the `orders` exchange.

---

## Configuration reference

### Connection strings

| Key | Default | Description |
|-----|---------|-------------|
| `ConnectionStrings:Outbox` | `Data Source=outbox.db` | SQLite outbox database path |

### RabbitMQ options (`Events:RabbitMq:*`)

| Key | Default | Description |
|-----|---------|-------------|
| `ConnectionString` | `amqp://guest:guest@localhost:5672` | AMQP broker URI |
| `ExchangeName` | `orders` | Default exchange (per-event `[AmqpExchange]` overrides this) |
| `PersistentMessages` | `true` | Survive broker restarts |
| `PublisherConfirms` | `true` | Wait for broker `ACK` |
| `ConfirmTimeout` | `00:00:05` | Timeout for broker `ACK` |
| `ClientName` | `order-service-outbox` | Connection label shown in management UI |

Override any value with the standard environment-variable convention:

```bash
Events__RabbitMq__ConnectionString=amqp://user:pass@rabbitmq:5672 dotnet run
```

---

## When to use this topology

| | In-process relay (this sample) | External relay ([outbox-relay](outbox-relay-masstransit.md)) |
|---|---|---|
| **Simplicity** | ✅ Single process, single deployment unit | ❌ Two separate processes |
| **Fault isolation** | ❌ Relay crash affects the API process | ✅ Relay restarts independently |
| **Independent scaling** | ❌ API and relay scale together | ✅ Scale relay separately |
| **Transport abstraction** | RabbitMQ wired directly | MassTransit (broker-agnostic) |

Choose the in-process topology when you want the simplest setup for small-to-medium workloads and co-located relay downtime is acceptable.

---

## Related documentation

- [Transactional Outbox Channel](../publishers/reliability/outbox/README.md)
- [RabbitMQ Channel](../publishers/rabbitmq.md)
- [Typed Channels](../publishers/typed-channels.md)
- [Event Annotations](../concepts/event-annotations.md)
