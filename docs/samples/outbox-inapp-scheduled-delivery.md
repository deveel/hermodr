# Sample: OrderService — In-Process Outbox with Scheduled Delivery

**Location:** [`samples/outbox-inapp/OrderService.InAppOutboxScheduling/`](https://github.com/deveel/deveel.events/tree/main/samples/outbox-inapp/OrderService.InAppOutboxScheduling)  
**Framework:** ASP.NET Core 9 Minimal API  
**Transport:** RabbitMQ — `Deveel.Events.Publisher.RabbitMq`  
**Pattern:** Transactional Outbox with delayed transport delivery

---

## Overview

This sample demonstrates a specific semantic: the domain event already occurred, but its **broker delivery** is deferred.

The API creates an `Order` immediately and publishes `OrderCreated` through the outbox using `OutboxPublishOptions.ScheduleDeliveryAt`. The outbox row is persisted immediately, and the in-process relay waits until the scheduled UTC timestamp before forwarding the event to RabbitMQ.

```text
POST /orders
    -> OrderCreated recorded now
    -> DbOutboxMessage persisted now
    -> relay forwards later when ScheduleDeliveryAt is due
```

---

## What it shows

### 1. Delivery scheduling via Outbox

Business code maps request input to outbox publish options:

```csharp
var publishOptions = request.ScheduleEventDeliveryAt is { } scheduleDeliveryAt
    ? new OutboxPublishOptions { ScheduleDeliveryAt = scheduleDeliveryAt }
    : null;

await _publisher.PublishAsync(new OrderCreated
{
    OrderId = order.Id,
    CustomerId = order.CustomerId,
    TotalAmount = order.TotalAmount,
    CreatedAt = order.CreatedAt,
    Items = /* ... */
}, publishOptions, ct);
```

### 2. Immediate persistence, delayed forwarding

The outbox stores the event row immediately, setting `NextRetryAt` to the scheduled delivery time. The relay then treats the row as ineligible until it becomes due.

### 3. Inspectable outbox state

The sample exposes:

```text
GET /outbox/messages
```

so you can observe `Pending`, `NextRetryAt`, and later `Delivered` without opening SQLite manually.

---

## Running the sample

```bash
cd samples/outbox-inapp/OrderService.InAppOutboxScheduling

docker compose up -d

dotnet run --project OrderService.InAppOutboxScheduling.csproj
```

Generate a timestamp a few seconds in the future:

```bash
SCHEDULE_AT=$(python3 - <<'PY'
from datetime import datetime, timezone, timedelta
print((datetime.now(timezone.utc) + timedelta(seconds=20)).isoformat().replace('+00:00', 'Z'))
PY
)
```

Create a scheduled-delivery order event:

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

Inspect the outbox immediately:

```bash
curl -s http://localhost:5000/outbox/messages | jq
```

Wait until due time, then inspect again:

```bash
sleep 25
curl -s http://localhost:5000/outbox/messages | jq
```

---

## Why this sample exists

This sample is intentionally different from “future domain events”. It models:

- event occurred now
- outbox record exists now
- transport delivery happens later

That keeps the event semantics intact while still supporting infrastructure-level delayed fan-out.

---

## Related docs

- [Transactional Outbox](../publishers/outbox.md)
- [RabbitMQ Publisher](../publishers/rabbitmq.md)
- [Samples overview](README.md)

