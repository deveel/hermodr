# Transactional Outbox

The **Transactional Outbox** pattern ensures that domain events are never lost, even if the message broker is unavailable at the time of publishing.

## Why Use the Outbox Pattern?

In a distributed system, the moment between committing a domain change and dispatching a message to a broker is a window of potential data loss:

- If the application crashes **after the commit** but **before the send**, the event is silently dropped
- If it crashes **after the send** but **before the commit**, the broker receives an event for a change that never happened

The Transactional Outbox pattern closes that window:

```
┌───────────────────────────────────────────────────────────────┐
│  Application                                                  │
│                                                               │
│  ┌─────────────────┐   same transaction   ┌───────────────┐   │
│  │  Domain entity  │ ──────────────────── │  Outbox table │   │
│  └─────────────────┘                      └───────┬───────┘   │
└───────────────────────────────────────────────────│───────────┘
                                                     │  poll
                                           ┌─────────▼─────────┐
                                           │   Relay service   │
                                           └─────────┬─────────┘
                                                     │  publish
                                           ┌─────────▼─────────┐
                                           │   Message broker  │
                                           │ (RabbitMQ / ASB)  │
                                           └───────────────────┘
```

### Benefits

| Benefit | Description |
|---------|-------------|
| **Atomic writes** | Domain change and outbox record are saved in the same transaction |
| **Decoupled durability** | Writing to the outbox is fast; relay handles delivery asynchronously |
| **Guaranteed delivery** | Relay can retry indefinitely until the broker acknowledges receipt |
| **Broker independence** | Application doesn't need the broker to be available at publish time |

## How It Works

### 1. Publish Phase (Synchronous)

When `IEventPublisher.PublishAsync` is called:

1. Event is serialized to a `CloudEvent`
2. Outbox record is created with status `Pending`
3. Both the domain entity and outbox record are committed in the **same transaction**
4. Call returns immediately — no network call to the broker yet

```csharp
public async Task PlaceOrderAsync(PlaceOrderCommand cmd, CancellationToken ct = default)
{
    var order = new Order { CustomerId = cmd.CustomerId, Total = cmd.Total };
    await _db.Orders.AddAsync(order, ct);

    // Writes only to the outbox table — fast and atomic
    await _publisher.PublishAsync(new OrderPlaced
    {
        OrderId    = order.Id,
        CustomerId = order.CustomerId,
        Total      = order.Total,
    }, cancellationToken: ct);

    // Commits both the order row and the outbox record atomically
    await _db.SaveChangesAsync(ct);
}
```

### 2. Relay Phase (Asynchronous)

A background relay service polls the outbox table:

1. Queries for `Pending` records whose `NextRetryAt` time has passed
2. Claims each record by setting status to `Sending`
3. Publishes the event to the actual broker (RabbitMQ, Azure Service Bus, etc.)
4. On success: marks record as `Delivered`
5. On transient error: schedules a retry with `NextRetryAt`

## Timing Model

Outbox persistence tracks multiple timestamps with different meanings:

| Field | Meaning |
|-------|---------|
| `EventTime` / `CloudEvent.time` | When the business event occurred (immutable event history) |
| `CreatedAt` | When the outbox row was written |
| `LastStatusAt` | When the status last changed (`Sending`, `Delivered`, `Failed`) |
| `NextRetryAt` | Earliest time the relay may attempt delivery again |

> **Important:** Treat `EventTime` as immutable event history and `NextRetryAt`/`LastStatusAt` as transport workflow metadata. This separation is critical for reliable auditing and SLA analysis.

## Message Lifecycle

Each outbox record moves through a well-defined set of states:

```
           Publish call
               │
               ▼
         ┌──────────┐
         │ Pending  │ ◄──────────────────────────────┐
         └────┬─────┘                                │
              │ relay claims                         │  back-off & retry
              ▼                                      │
         ┌──────────┐                                │
         │ Sending  │                                │
         └────┬─────┘                                │
     ┌────────┴────────┐                             │
success           transient error       max retries exceeded
     │                 │                             │
     ▼                 └─────────────────────────────┘
┌───────────┐                                         │
│ Delivered │                                    ┌────▼────┐
└───────────┘                                    │ Failed  │
                                                 └─────────┘
```

| Status | Meaning |
|--------|---------|
| `Pending` | Record written, waiting for relay to pick up |
| `Sending` | Relay has claimed the record and is attempting delivery |
| `Delivered` | Broker has acknowledged receipt; record can be archived |
| `Failed` | All retries exhausted; manual intervention needed |

## Next Steps

| Page | Description |
|------|-------------|
| [Installation](installation.md) | Install packages and dependencies |
| [Components](components.md) | Implement outbox entity, factory, and store |
| [EF Core Integration](ef-core-integration.md) | Use the ready-made EF Core implementation |
| [Registration](registration.md) | Wire up outbox in dependency injection |
| [Relay Service](relay.md) | Configure same-process or cross-process relay |
| [Lifecycle](lifecycle.md) | Deep dive into message states and transitions |
| [Example](example.md) | End-to-end walkthrough with OrderService |

## Related Pages

- [Reliability Patterns Overview](../README.md)
- [Dead-Letter Handling](../dead-letter/README.md)
- [Delivery Log](../delivery-log/README.md)
- [Named Channels](../../named-channels.md)
