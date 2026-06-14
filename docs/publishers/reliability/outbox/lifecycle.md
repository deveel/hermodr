# Outbox Message Lifecycle

Outbox records move through a well-defined lifecycle that tracks their journey from creation to final delivery.

## Lifecycle States

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

## State Reference

### Pending

**Entry Points:**
- Initial publish via `IOutboxMessageStore.AddAsync`
- Retry scheduled via `SetRetryAsync`
- Deferred delivery via `SetDeferredAsync`

**Meaning:** The outbox record has been written and is waiting to be picked up by the relay.

**Characteristics:**
- `Status = OutboxMessageStatus.Pending`
- `NextRetryAt` may be `null` (immediately eligible) or a future time (scheduled)
- Record is visible to the relay's `GetPendingMessagesAsync` query

**Transition To:**
- `Sending` — when relay claims the message for delivery

---

### Sending

**Entry Point:**
- Relay claims message via `SetSendingAsync`

**Meaning:** The relay has claimed the record and is actively attempting delivery.

**Characteristics:**
- `Status = OutboxMessageStatus.Sending`
- Message is excluded from `GetPendingMessagesAsync` queries
- Prevents concurrent relay instances from processing the same message

**Transition To:**
- `Delivered` — on successful broker acknowledgment
- `Pending` — on transient error with retry scheduled
- `Failed` — on permanent error or retry limit exceeded

---

### Delivered

**Entry Point:**
- Successful delivery via `SetDeliveredAsync`

**Meaning:** The transport channel has acknowledged receipt of the event.

**Characteristics:**
- `Status = OutboxMessageStatus.Delivered`
- `LastStatusAt` records the delivery completion time
- Record can be archived or deleted according to retention policy

**Transition To:**
- None — this is a terminal state

**Post-Delivery:**
- Records can be purged after a retention period (e.g., 7 days)
- Consider archiving to cold storage for audit purposes before deletion

---

### Failed

**Entry Point:**
- Retry limit exceeded via `SetFailedAsync`

**Meaning:** All delivery attempts have been exhausted; manual intervention is needed.

**Characteristics:**
- `Status = OutboxMessageStatus.Failed`
- `RetryCount` equals the configured maximum
- `ErrorMessage` contains the last failure reason
- Requires human intervention or dead-letter processing

**Transition To:**
- `Pending` — if manually reset for reprocessing (custom operation)

**Handling Failed Messages:**
1. Investigate the `ErrorMessage` to understand the root cause
2. Fix the underlying issue (e.g., broker configuration, serialization problem)
3. Reset the message to `Pending` status for reprocessing, or
4. Move to a dead-letter queue for manual handling

---

## Timing Metadata

Outbox records track multiple timestamps with different meanings:

| Field | Type | Meaning | Mutability |
|-------|------|---------|------------|
| `EventTime` | `DateTimeOffset` | When the business event occurred (`CloudEvent.time`) | Immutable |
| `CreatedAt` | `DateTimeOffset` | When the outbox row was written | Immutable |
| `LastStatusAt` | `DateTimeOffset` | When the status last changed | Mutable |
| `NextRetryAt` | `DateTimeOffset?` | Earliest time for next delivery attempt | Mutable |

### EventTime vs Workflow Timing

> **Important:** Treat `EventTime` as immutable event history and `NextRetryAt`/`LastStatusAt` as transport workflow metadata. This separation is critical for:
>
> - **Auditing** — Accurate record of when events actually occurred
> - **SLA Analysis** — Measure actual delivery latency (`LastStatusAt - EventTime`)
> - **Replay** — Reconstruct event chronology independent of delivery attempts

### Time Abstraction

All timestamp operations use the configured clock abstraction (`ISystemTime` in EF repositories) rather than direct `DateTimeOffset.UtcNow` calls:

```csharp
public class EntityOutboxMessageRepository<TMessage> : IOutboxMessageStore<TMessage>
    where TMessage : DbOutboxMessage
{
    private readonly ISystemTime _systemTime;

    public async Task SetRetryAsync(TMessage message, Exception error, 
        DateTimeOffset nextRetryAt, CancellationToken ct = default)
    {
        message.Status = OutboxMessageStatus.Pending;
        message.ErrorMessage = error.Message;
        message.RetryCount++;
        message.NextRetryAt = nextRetryAt;
        message.LastStatusAt = _systemTime.UtcNow;  // Testable!
        
        await _db.SaveChangesAsync(ct);
    }
}
```

**Benefits:**
- **Testability** — Inject a frozen clock in tests to assert exact timestamp values
- **Consistency** — All timestamps use the same time source
- **Flexibility** — Swap implementations for different time zones or testing scenarios

---

## Retry Behavior

### Retry Count

The `RetryCount` field tracks how many delivery attempts have been made:

- **Initial publish:** `RetryCount = 0`
- **After first failure:** `RetryCount = 1`
- **After second failure:** `RetryCount = 2`
- **And so on...**

### Retry Scheduling

When a delivery fails, the relay schedules the next attempt based on a back-off strategy:

```
Attempt 1: Immediate (NextRetryAt = null or past)
Attempt 2: After 30 seconds
Attempt 3: After 1 minute
Attempt 4: After 2 minutes
Attempt 5: After 5 minutes
...
Max reached: Move to Failed status
```

The exact back-off strategy is configurable in your relay implementation.

### NextRetryAt

The `NextRetryAt` field controls when a message becomes eligible for retry:

- **`null`** — Immediately eligible for delivery
- **Future time** — Not eligible until that time has passed
- **Past time** — Immediately eligible (same as `null`)

**Query Pattern:**
```sql
SELECT * FROM OutboxMessages
WHERE Status = 'Pending'
  AND (NextRetryAt IS NULL OR NextRetryAt <= @Now)
ORDER BY CreatedAt ASC
```

---

## Status Transitions

### Complete Transition Table

| From | To | Trigger | Method |
|------|-----|---------|--------|
| — | `Pending` | Initial publish | `AddAsync` |
| `Pending` | `Sending` | Relay claims message | `SetSendingAsync` |
| `Sending` | `Delivered` | Successful delivery | `SetDeliveredAsync` |
| `Sending` | `Pending` | Transient error, retry scheduled | `SetRetryAsync` |
| `Sending` | `Failed` | Retry limit exceeded | `SetFailedAsync` |
| `Pending` | `Pending` | Deferred delivery scheduled | `SetDeferredAsync` |

### Deferred Delivery

The `SetDeferredAsync` method allows scheduling a message for future delivery without incrementing the retry count:

```csharp
// Schedule for delivery in 1 hour
var scheduledAt = DateTimeOffset.UtcNow.AddHours(1);
await _store.SetDeferredAsync(message, scheduledAt, ct);
```

**Use Cases:**
- Scheduled events (e.g., reminder emails)
- Time-based workflows
- Rate limiting (space out deliveries)

---

## Monitoring and Alerting

### Key Queries

```csharp
// Count messages by status
var statusCounts = await _db.OutboxMessages
    .GroupBy(m => m.Status)
    .Select(g => new { Status = g.Key, Count = g.Count() })
    .ToListAsync();

// Find messages stuck in Sending status (potential poison pills)
var stuckMessages = await _db.OutboxMessages
    .Where(m => m.Status == OutboxMessageStatus.Sending 
             && m.LastStatusAt < DateTimeOffset.UtcNow.AddMinutes(-30))
    .ToListAsync();

// Calculate average delivery latency
var avgLatency = await _db.OutboxMessages
    .Where(m => m.Status == OutboxMessageStatus.Delivered 
             && m.EventTime > DateTimeOffset.UtcNow.AddHours(-1))
    .AverageAsync(m => (m.LastStatusAt - m.CreatedAt).TotalSeconds);

// Find messages approaching retry limit
var nearFailure = await _db.OutboxMessages
    .Where(m => m.Status == OutboxMessageStatus.Pending 
             && m.RetryCount >= 3)  // Assuming max is 5
    .OrderByDescending(m => m.RetryCount)
    .ToListAsync();
```

### Alerting Recommendations

| Condition | Severity | Action |
|-----------|----------|--------|
| `Failed` count > 0 | Critical | Investigate root cause immediately |
| `Pending` count > 1000 for > 5 min | Warning | Check relay health, broker connectivity |
| Messages stuck in `Sending` for > 30 min | Warning | Potential poison pill, manual intervention needed |
| Average latency > 5 minutes | Warning | Consider scaling relay or optimizing broker |
| Retry rate > 10% | Warning | Investigate transient failure patterns |

---

## Related Pages

- [Transactional Outbox Overview](README.md)
- [Relay Service](relay.md)
- [Example Walkthrough](example.md)
