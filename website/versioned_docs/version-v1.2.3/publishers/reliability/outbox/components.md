# Outbox Components

To use the outbox channel, you need to provide three components that adapt the library to your persistence technology:

1. **Outbox Message Entity** — The database row that stores the event
2. **Message Factory** — Creates outbox records from `CloudEvent` instances
3. **Message Store** — Repository for persisting and querying outbox records

> **Entity Framework Core users:** If you're using the `Hermodr.Publisher.Outbox.EntityFramework` package, you can skip most of this — the ready-made `DbOutboxMessage` entity and `EntityOutboxMessageRepository` handle everything. See [EF Core Integration](ef-core-integration.md) for the recommended approach.

---

## 1. Outbox Message Entity

Your entity class must implement `IOutboxMessage`. It acts as the database row that the relay service reads and the channel writes.

### Required Properties

| Property | Type | Purpose |
|----------|------|---------|
| `Event` | `CloudEvent` | The event payload to deliver |
| `Status` | `OutboxMessageStatus` | Tracks delivery lifecycle |
| `ErrorMessage` | `string?` | Last failure reason |
| `RetryCount` | `int` | Number of delivery attempts |
| `NextRetryAt` | `DateTimeOffset?` | Earliest time for next retry |

### Minimal Entity Example

```csharp
using CloudNative.CloudEvents;
using Hermodr.Publisher.Outbox;

public class OrderOutboxMessage : IOutboxMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    // IOutboxMessage — the CloudEvent payload
    public CloudEvent Event { get; set; } = default!;

    public OutboxMessageStatus Status { get; set; } = OutboxMessageStatus.Pending;
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public DateTimeOffset? NextRetryAt { get; set; }
}
```

> **Important:** Direct `IOutboxMessage` implementation is only suited for custom persistence strategies (non-EF backends such as MongoDB, Dapper, etc.). For EF Core, use `DbOutboxMessage` from the Entity Framework package.

---

## 2. Message Factory

`IOutboxMessageFactory<TMessage>` converts a `CloudEvent` into a new outbox entity instance. The factory should be **stateless** and **allocation-light**.

### Interface

```csharp
public interface IOutboxMessageFactory<TMessage>
{
    TMessage Create(CloudEvent cloudEvent, OutboxPublishOptions? options = null);
}
```

### Example Implementation

```csharp
public class OrderOutboxMessageFactory : IOutboxMessageFactory<OrderOutboxMessage>
{
    public OrderOutboxMessage Create(CloudEvent cloudEvent, OutboxPublishOptions? options = null)
        => new() 
        { 
            Event = cloudEvent,
            Status = OutboxMessageStatus.Pending
        };
}
```

The `options` parameter carries any per-call publish options (channel name, content-type overrides) in case you need to store them alongside the entity.

---

## 3. Message Store

`IOutboxMessageStore<TMessage>` provides persistence operations for outbox messages.

### Interface

```csharp
public interface IOutboxMessageStore<TMessage>
{
    Task AddAsync(TMessage message, CancellationToken cancellationToken = default);
    Task<OutboxMessageStatus> GetStatusAsync(TMessage message, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TMessage>> GetPendingMessagesAsync(int? limit = null, CancellationToken cancellationToken = default);
    Task SetSendingAsync(TMessage message, CancellationToken cancellationToken = default);
    Task SetDeliveredAsync(TMessage message, CancellationToken cancellationToken = default);
    Task SetDeferredAsync(TMessage message, DateTimeOffset scheduledAt, CancellationToken cancellationToken = default);
    Task SetRetryAsync(TMessage message, Exception error, DateTimeOffset nextRetryAt, CancellationToken cancellationToken = default);
    Task SetFailedAsync(TMessage message, Exception error, CancellationToken cancellationToken = default);
}
```

### Method Reference

| Method | When Called | What to Do |
|--------|-------------|------------|
| `AddAsync` | On publish | Persist a new `Pending` outbox record |
| `GetStatusAsync` | On-demand status check | Return current status |
| `GetPendingMessagesAsync` | Every relay tick | Return `Pending` records ready for delivery |
| `SetSendingAsync` | Before delivery attempt | Set `Status = Sending` to claim the record |
| `SetDeliveredAsync` | After successful delivery | Set `Status = Delivered` |
| `SetDeferredAsync` | After initial deferral | Keep `Status = Pending`, set `NextRetryAt` |
| `SetRetryAsync` | After transient failure | Set `Status = Pending`, increment `RetryCount`, set `NextRetryAt` |
| `SetFailedAsync` | When retry limit exceeded | Set `Status = Failed` |

### Implementation Example (Simplified)

```csharp
public class MyOutboxMessageStore : IOutboxMessageStore<OrderOutboxMessage>
{
    private readonly MyDbContext _db;

    public MyOutboxMessageStore(MyDbContext db) => _db = db;

    public async Task AddAsync(OrderOutboxMessage message, CancellationToken ct = default)
    {
        // Don't call SaveChangesAsync here - let the caller commit the unit of work
        _db.OutboxMessages.Add(message);
    }

    public async Task<IReadOnlyList<OrderOutboxMessage>> GetPendingMessagesAsync(int? limit = null, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var query = _db.OutboxMessages
            .Where(m => m.Status == OutboxMessageStatus.Pending 
                     && (m.NextRetryAt == null || m.NextRetryAt <= now))
            .OrderBy(m => m.CreatedAt);

        if (limit.HasValue)
            query = query.Take(limit.Value);

        return await query.ToListAsync(ct);
    }

    public async Task SetDeliveredAsync(OrderOutboxMessage message, CancellationToken ct = default)
    {
        message.Status = OutboxMessageStatus.Delivered;
        message.LastStatusAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    // ... implement other methods similarly
}
```

> **Atomicity Tip:** The `AddAsync` method is called from within the same `DbContext` scope as the domain entity write. For EF Core, you should **not** call `SaveChangesAsync` inside it — let the caller flush the unit of work. The state-transition methods (`SetSendingAsync`, etc.) are called by the relay service outside of a business transaction, so they should persist immediately.

---

## Entity Framework Core Integration

For EF Core users, the `Hermodr.Publisher.Outbox.EntityFramework` package eliminates all this boilerplate:

- **`DbOutboxMessage`** — Complete, ready-to-use entity with EF mappings
- **`EntityOutboxMessageRepository<TMessage>`** — Ready-made repository
- **`WithEntityFramework()`** — Wires everything in one call

See [EF Core Integration](ef-core-integration.md) for the recommended approach.

---

## Related Pages

- [Transactional Outbox Overview](README.md)
- [EF Core Integration](ef-core-integration.md)
- [Registration](registration.md)
