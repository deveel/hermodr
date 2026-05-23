# Sample: OrderService — Split Outbox + MassTransit RabbitMQ

**Location:** [`samples/outbox-relay/`](https://github.com/deveel/hermodr/tree/main/samples/outbox-relay)  
**Framework:** ASP.NET Core 9 Minimal API · .NET 9 Worker Service  
**Transport:** MassTransit over RabbitMQ — `Hermodr.Publisher.MassTransit`  
**Pattern:** Transactional Outbox (external relay process)

---

## Overview

This sample demonstrates the **split Transactional Outbox** pattern using **two separate processes** that share a SQLite database:

| Process | Project | Role |
|---------|---------|------|
| Minimal API | `OrderService.Api` | Accepts HTTP requests; writes CloudEvents to the outbox only |
| Console worker | `OrderService.RelayWorker` | Polls the shared outbox; forwards events to RabbitMQ via **MassTransit** |

The API has **no knowledge of RabbitMQ or MassTransit** — it only writes to the outbox. This clean separation means the transport layer can be swapped (e.g. Azure Service Bus, Amazon SQS) by changing the worker, without touching the API.

### Architecture

```
HTTP request
    │
    ▼
OrderService.Api
    │  IEventPublisher.PublishAsync(event)
    ▼
OutboxPublishChannel  ──── INSERT into shared SQLite (outbox.db)

                    ┌── separate process ──────────────────────────────┐
                    │  OrderService.RelayWorker                        │
                    │                                                  │
                    │  OutboxRelayService (BackgroundService)          │
                    │      polls outbox.db every 5 s                   │
                    │      │                                           │
                    │      ▼                                           │
                    │  MassTransitPublishChannel                       │
                    │      │                                           │
                    └──────│───────────────────────────────────────────┘
                           │
                           ▼
                    RabbitMQ broker
```

---

## What this sample demonstrates

### 1. API — outbox write path only

`Program.cs` in `OrderService.Api` calls `.AddEntityFrameworkOutbox()` with `.WithFactory<>()` but **without** `.WithRelay()`:

```csharp
var events = builder.Services.AddEventPublisher(options =>
{
    options.Source = new Uri("https://example.com/services/order-service");
});

events
    .AddEntityFrameworkOutbox(opts =>
        opts.UseSqlite(builder.Configuration.GetConnectionString("Outbox")))
    .WithFactory<OrderOutboxMessageFactory>();
    // ↑ No .WithRelay() — forwarding is handled by the external relay worker
```

The API project references **only** `Hermodr.Publisher.Outbox.EntityFramework` — there is no dependency on any transport package.

### 2. Worker — relay + MassTransit

`Program.cs` in `OrderService.RelayWorker` first configures MassTransit with the RabbitMQ transport, then registers the outbox channel with `.WithRelay()` and adds the MassTransit publish channels:

```csharp
// ── MassTransit ──────────────────────────────────────────────────────────
builder.Services.AddMassTransit(mt =>
{
    mt.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(host, h =>
        {
            h.Username(username);
            h.Password(password);
        });

        cfg.ConfigureEndpoints(ctx);
    });
});

// ── Hermodr outbox relay → MassTransit ─────────────────────────────
var events = builder.Services.AddEventPublisher(options =>
{
    options.Source = new Uri("https://example.com/services/order-service");
});

// Outbox repository — same database as the API
events
    .AddEntityFrameworkOutbox(opts =>
        opts.UseSqlite(builder.Configuration.GetConnectionString("Outbox")))
    .WithFactory<OrderOutboxMessageFactory>()
    .WithRelay(opts =>
    {
        opts.Interval     = TimeSpan.FromSeconds(5);
        opts.MaxBatchSize = 50;
    });

// MassTransit channels — used only by the relay
events
    .AddMassTransit()                  // generic catch-all channel
    .AddMassTransit<OrderCreated>()    // typed per-event channels
    .AddMassTransit<OrderConfirmed>()
    .AddMassTransit<OrderShipped>()
    .AddMassTransit<OrderDelivered>()
    .AddMassTransit<OrderCancelled>();
```

MassTransit must be registered **before** the Hermodr channels so that `IPublishEndpoint` and `ISendEndpointProvider` are available in DI when the `MassTransitPublishChannel` resolves them.

### 3. Event types — no AMQP coupling in the API

Event classes in `OrderService.Api` carry only the `[Event]` annotation:

```csharp
[Event("order.created", "1.0", Description = "A new order was placed by a customer")]
public sealed class OrderCreated
{
    public Guid OrderId { get; set; }
    public string CustomerId { get; set; } = default!;
    public IReadOnlyList<OrderCreatedItem> Items { get; set; } = [];
    public decimal TotalAmount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
```

The worker defines only stub event marker types — topic routing is handled by MassTransit's topology, not by AMQP annotations:

```csharp
[Event("order.created", "1.0")]
public sealed class OrderCreated { }
```

### 4. Shared outbox database

Both processes point at the same SQLite file:

| Process | `ConnectionStrings:Outbox` |
|---------|---------------------------|
| API | `Data Source=outbox.db` (written in the API working directory) |
| Worker | `Data Source=../OrderService.Api/outbox.db` (relative path) |

> **Production note:** replace SQLite with SQL Server or PostgreSQL when running the two processes on different hosts or containers. SQLite file-locking is not safe for concurrent cross-process writes under high load.

---

## Prerequisites

| Tool | Version |
|------|---------|
| [.NET SDK](https://dotnet.microsoft.com/download) | 9.0 + |
| [Docker](https://www.docker.com/) | any recent version |

---

## Running the sample

**1. Start RabbitMQ**

```bash
cd samples/outbox-relay
docker compose up -d
```

The RabbitMQ management UI is available at <http://localhost:15672> (`guest` / `guest`).

**2. Start the API** (terminal 1)

```bash
cd samples/outbox-relay/OrderService.Api
dotnet run
```

The API listens on `http://localhost:5000`. The SQLite database (`outbox.db`) is created automatically.

**3. Start the relay worker** (terminal 2)

```bash
cd samples/outbox-relay/OrderService.RelayWorker
dotnet run
```

The worker connects to the same `outbox.db` via the relative path in `appsettings.json` and starts polling every 5 seconds.

> **Tip:** override the path with an environment variable if the working directories differ:
>
> ```bash
> ConnectionStrings__Outbox="Data Source=/absolute/path/outbox.db" dotnet run
> ```

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

Within ~5 seconds after each request, the relay worker logs a `Sending` update followed by a `Sent` confirmation. The messages appear in the [RabbitMQ management UI](http://localhost:15672) as MassTransit-formatted messages on the `Hermodr` exchange topology.

---

## Configuration reference

### API (`OrderService.Api/appsettings.json`)

| Key | Default | Description |
|-----|---------|-------------|
| `ConnectionStrings:Outbox` | `Data Source=outbox.db` | SQLite outbox database (written by the API) |

### Worker (`OrderService.RelayWorker/appsettings.json`)

| Key | Default | Description |
|-----|---------|-------------|
| `ConnectionStrings:Outbox` | `Data Source=../OrderService.Api/outbox.db` | Path to the shared SQLite database |
| `Events:MassTransit:RabbitMq:Host` | `localhost` | RabbitMQ broker hostname |
| `Events:MassTransit:RabbitMq:Username` | `guest` | RabbitMQ username |
| `Events:MassTransit:RabbitMq:Password` | `guest` | RabbitMQ password |

---

## Comparison with the in-process topology

| | In-process relay ([outbox-inapp](outbox-inapp-rabbitmq.md)) | External relay (this sample) |
|---|---|---|
| **Process count** | 1 — API + relay in the same host | 2 — API and relay run independently |
| **Transport** | RabbitMQ (direct AMQP channel) | **MassTransit** (broker-agnostic abstraction) |
| **API dependency on transport** | Yes — `Hermodr.Publisher.RabbitMq` | **No** — API only refs the outbox package |
| **Relay restart** | Restarts the entire API process | Relay restarts independently |
| **Independent scaling** | ❌ Relay scales with the API | ✅ Scale relay separately |
| **Swap broker later** | Requires API changes | Change worker only |

Choose the external-relay topology when fault isolation, independent scaling, or transport-agnosticism are priorities.

---

## Related documentation

- [Transactional Outbox Channel](../publishers/outbox.md#cross-process-deployment)
- [MassTransit Channel](../publishers/masstransit.md)
- [Typed Channels](../publishers/typed-channels.md)
- [Event Publisher](../concepts/event-publisher.md)
