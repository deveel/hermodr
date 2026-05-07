# Order Service — Scheduled Delivery Outbox Sample

This sample shows how to use **Deveel.Events** to record a domain event immediately and delay only its **transport delivery** through the **Transactional Outbox** pattern.

The business fact (`order.created`) happens now. The event is written to the SQLite outbox immediately. If the caller provides a future `scheduleEventDeliveryAt` timestamp, the relay waits until that UTC time before forwarding the event to RabbitMQ.

## What it demonstrates

| Concern | Implementation |
|---------|---------------|
| Domain event | `OrderCreated` |
| Delayed delivery API | `OutboxPublishOptions.ScheduleDeliveryAt` |
| Outbox store | SQLite via Entity Framework Core (`outbox-scheduling.db`) |
| Relay | `OutboxRelayService<DbOutboxMessage>` in the same ASP.NET process |
| Transport | RabbitMQ |
| Visibility | `/outbox/messages` endpoint to inspect `NextRetryAt` / status |

### Delivery flow

```text
HTTP request
    │
    ▼
OrderManagementService.CreateAsync(...)
    │
    ▼
IEventPublisher.PublishAsync(new OrderCreated, new OutboxPublishOptions
{
    ScheduleDeliveryAt = <future UTC timestamp>
})
    │
    ▼
OutboxPublishChannel  ──── saves CloudEvent to SQLite immediately
                                  │
                                  ├── record exists even if broker is down later
                                  │
                                  ▼
OutboxRelayService polls every 1 s
    │
    ├── if NextRetryAt > now  → keep Pending
    └── if NextRetryAt <= now → forward to RabbitMQ
```

## Prerequisites

| Tool | Version |
|------|---------|
| .NET SDK | 9.0+ |
| Docker + Compose | any recent version |
| Python | 3.x (only used below to generate a UTC timestamp) |

## Running the sample

```bash
cd samples/outbox-inapp/OrderService.InAppOutboxScheduling

docker compose up -d

dotnet run --project OrderService.InAppOutboxScheduling.csproj
```

OpenAPI is available in Development mode at `https://localhost:<port>/openapi`.

## Walkthrough

### 1. Create a future UTC timestamp

```bash
SCHEDULE_AT=$(python3 - <<'PY'
from datetime import datetime, timezone, timedelta
print((datetime.now(timezone.utc) + timedelta(seconds=20)).isoformat().replace('+00:00', 'Z'))
PY
)

echo "$SCHEDULE_AT"
```

### 2. Create an order whose event delivery is delayed

```bash
curl -s -X POST http://localhost:5000/orders \
  -H "Content-Type: application/json" \
  -d "{
    \"customerId\": \"cust-42\",
    \"scheduleEventDeliveryAt\": \"$SCHEDULE_AT\",
    \"items\": [
      { \"productId\": \"sku-001\", \"productName\": \"Widget\", \"quantity\": 2, \"unitPrice\": 9.99 }
    ]
  }" | jq
```

The order is created immediately.
The `order.created` event is also persisted immediately in SQLite, but RabbitMQ delivery is deferred.

### 3. Inspect the outbox right away

```bash
curl -s http://localhost:5000/outbox/messages | jq
```

You should see a recent row similar to:

```json
{
  "eventType": "order.created",
  "status": "Pending",
  "retryCount": 0,
  "nextRetryAt": "2026-05-06T12:34:56Z"
}
```

`nextRetryAt` is the scheduled transport-delivery time.

### 4. Wait until the due time and inspect again

```bash
sleep 25
curl -s http://localhost:5000/outbox/messages | jq
```

Once the relay tick runs after the due time, the message should move through:

```text
Pending -> Sending -> Delivered
```

## Why this sample matters

This sample intentionally demonstrates **delayed delivery**, not “future domain occurrence”.

- the order is created **now**
- the `order.created` event exists **now**
- only broker delivery is delayed
- the outbox guarantees the event record is preserved even if delivery later fails

That makes the feature useful for:

- throttled downstream consumption
- maintenance windows
- partner cutover timing
- delayed fan-out while preserving an auditable event record

## Configuration

| Key | Default | Description |
|-----|---------|-------------|
| `ConnectionStrings:Outbox` | `Data Source=outbox-scheduling.db` | SQLite outbox path |
| `Events:RabbitMq:ConnectionString` | `amqp://guest:guest@localhost:5672` | AMQP broker URI |
| relay interval | `1 s` in `Program.cs` | how often due messages are checked |

## Files to inspect

| File | Purpose |
|------|---------|
| `Program.cs` | registers outbox + relay + RabbitMQ |
| `Services/OrderManagementService.cs` | maps request scheduling to `OutboxPublishOptions.ScheduleDeliveryAt` |
| `Endpoints/OrderEndpoints.cs` | exposes `/outbox/messages` for inspection |
| `Infrastructure/OrderOutboxMessageFactory.cs` | persists CloudEvents to `DbOutboxMessage` |

## Related documentation

- [Transactional Outbox Channel](../../../docs/publishers/outbox.md)
- [Sample docs page](../../../docs/samples/outbox-inapp-scheduled-delivery.md)
