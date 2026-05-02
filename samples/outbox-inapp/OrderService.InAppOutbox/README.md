# Order Service — Outbox Pattern Sample

This sample shows how to use **Deveel.Events** to implement the
[Transactional Outbox pattern](https://microservices.io/patterns/data/transactional-outbox.html)
inside a single ASP.NET Minimal API process.

## What it demonstrates

| Concern | Implementation |
|---------|---------------|
| Domain events | `OrderCreated`, `OrderConfirmed`, `OrderShipped`, `OrderDelivered`, `OrderCancelled` |
| Outbox store | SQLite via Entity Framework Core (`outbox.db`) |
| Relay | `OutboxRelayService<DbOutboxMessage>` — a `BackgroundService` in the **same process** |
| Transport | RabbitMQ (one typed channel per event) |

### Request flow

```
HTTP request
    │
    ▼
OrderManagementService.PublishAsync(event)
    │
    ▼
OutboxPublishChannel  ──── atomically saves CloudEvent to SQLite (outbox.db)
                                  │
              ┌───────────────────┘  (polled every 5 s)
              │
              ▼
OutboxRelayService  (BackgroundService — same process)
    │  reads pending rows, marks them Sending
    │
    ▼
RabbitMQ channels  ──── forwards CloudEvent to the broker
```

Because every call to `IEventPublisher.PublishAsync` is intercepted by
`OutboxPublishChannel` — which writes to SQLite — the business code never
talks to RabbitMQ directly. If the broker is temporarily unavailable the
order request still succeeds; the relay will retry on the next polling tick.

The `OutboxPublishChannel` detects the `OutboxRelayPublishOptions` signal
emitted by the relay and **short-circuits** (no re-persistence), preventing
an infinite loop in this same-process deployment.

## Prerequisites

| Tool | Version |
|------|---------|
| .NET SDK | 9.0+ |
| Docker + Compose | any recent version |

## Running the sample

```bash
# 1. Start RabbitMQ
docker compose up -d

# 2. Run the API (SQLite is created automatically on first start)
dotnet run --project OrderService.csproj
```

OpenAPI UI is available at `https://localhost:<port>/openapi` when in Development mode.

### Verify the outbox

The SQLite database (`outbox.db`) is created next to the binary.
You can inspect it with any SQLite browser:

```sql
SELECT id, event_type, status, retry_count, created_at
FROM   outbox_messages
ORDER  BY created_at DESC;
```

Expected status transitions per message:
`Pending` → `Sending` → `Delivered`

## Configuration

| Key | Default | Description |
|-----|---------|-------------|
| `ConnectionStrings:Outbox` | `Data Source=outbox.db` | SQLite connection string |
| `Events:RabbitMq:ConnectionString` | `amqp://guest:guest@localhost:5672` | AMQP broker URI |
| Relay interval | 5 s (hardcoded in `Program.cs`) | How often the relay polls for pending messages |

Override any setting via environment variable or user-secrets.

## Key differences from the `aspnet-publisher` sample

| | `aspnet-publisher` | `outbox-publisher` (this sample) |
|---|---|---|
| Event path | Direct → RabbitMQ | Business Logic → SQLite outbox → RabbitMQ |
| Broker unavailability | Publish call fails | Request succeeds; relay retries |
| Relay process | N/A | `BackgroundService` inside the same app |
| Persistence | None | `outbox.db` (SQLite) |

