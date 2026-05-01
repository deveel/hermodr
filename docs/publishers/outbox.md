# Transactional Outbox Channel

The `Deveel.Events.Publisher.Outbox` package implements the **Transactional Outbox** pattern for Deveel Events.  Instead of sending a `CloudEvent` directly to a broker, the publisher persists the event into the same transactional store as the business data.  A separate relay process then reads pending messages and forwards them to the real transport channel.

## Why use the Outbox pattern?

In a distributed system the moment between committing a domain change and dispatching a message to a broker is a window for data loss.  If the application crashes after the commit but before the send, the event is silently dropped.  If it crashes after the send but before the commit, the event is sent for a change that never happened.

The Transactional Outbox pattern closes that window:

1. **Write atomically** – save the domain change **and** the outbox record in the same database transaction.
2. **Relay independently** – a background process reads confirmed outbox records and delivers them to the broker.
3. **Guaranteed delivery** – if the relay crashes, it can restart and pick up exactly where it left off; the outbox record is only removed (or marked `Delivered`) once the broker acknowledges receipt.

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
                                          │ (RabbitMQ / ASB …)│
                                          └───────────────────┘
```

## Installation

```bash
dotnet add package Deveel.Events.Publisher.Outbox
```

## Core concepts

### `IOutboxMessage`

Every record written to the outbox store must implement `IOutboxMessage`.  The interface exposes the wrapped `CloudEvent` and tracks the message through its delivery lifecycle:

| Member | Type | Description |
|--------|------|-------------|
| `CloudEvent` | `CloudEvent` | The event payload to deliver |
| `Status` | `OutboxMessageStatus` | Current delivery state |
| `ErrorMessage` | `string?` | Failure reason (set on error) |
| `RetryCount` | `int` | Number of failed delivery attempts |
| `NextRetryAt` | `DateTimeOffset?` | Earliest time the relay should retry; `null` means ready immediately |

### `OutboxMessageStatus`

| Value | Meaning |
|-------|---------|
| `Pending` | Waiting to be picked up by the relay |
| `Sending` | Claimed by the relay – in flight |
| `Delivered` | Successfully forwarded to the transport |
| `Failed` | Permanently failed after exhausting retries |

### `IOutboxMessageFactory<TMessage>`

The factory converts a `CloudEvent` into a concrete `TMessage` entity ready for persistence.  Implement it to map event data to your persistence model (e.g., a database entity):

```csharp
public TMessage Create(CloudEvent cloudEvent, OutboxPublishOptions? options = null);
```

The returned entity should have `Status = OutboxMessageStatus.Pending`.

### `IOutboxMessageRepository<TMessage>`

Beyond the standard CRUD surface (`IRepository<TMessage, string>`), the repository exposes outbox-specific state-transition methods:

| Method | Description |
|--------|-------------|
| `GetPendingMessagesAsync(limit?, ct)` | Returns messages eligible for relay (Pending, `NextRetryAt` in the past or null) |
| `SetSendingAsync(msg, ct)` | Marks the message as claimed by the relay |
| `SetDeliveredAsync(msg, ct)` | Marks the message as successfully delivered |
| `SetRetryAsync(msg, error, nextRetryAt, ct)` | Records a transient failure and schedules a retry |
| `SetFailedAsync(msg, error, ct)` | Permanently marks the message as failed |

## Implementation guide

### Step 1 – implement `IOutboxMessage`

Create an entity class that your persistence layer can store.  The example below uses plain properties, but you can add any ORM-specific annotations your database provider needs:

```csharp
using CloudNative.CloudEvents;
using Deveel.Events;

public class OrderOutboxMessage : IOutboxMessage
{
    // Primary key used by IRepository<TMessage, string>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    // Persisted as JSON in the database
    public CloudEvent CloudEvent { get; set; } = default!;

    public OutboxMessageStatus Status { get; set; } = OutboxMessageStatus.Pending;
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public DateTimeOffset? NextRetryAt { get; set; }
}
```

### Step 2 – implement `IOutboxMessageFactory<TMessage>`

The factory is called once per `PublishAsync` call.  It should be **stateless** and **fast** — just wrap the event and return the entity:

```csharp
using CloudNative.CloudEvents;
using Deveel.Events;

public class OrderOutboxMessageFactory : IOutboxMessageFactory<OrderOutboxMessage>
{
    public OrderOutboxMessage Create(CloudEvent cloudEvent, OutboxPublishOptions? options = null)
    {
        return new OrderOutboxMessage
        {
            CloudEvent = cloudEvent,
            Status     = OutboxMessageStatus.Pending,
        };
    }
}
```

### Step 3 – implement `IOutboxMessageRepository<TMessage>`

The repository must persist the outbox record **inside the same unit of work** as your domain entity so that both writes commit or roll back together:

```csharp
using Deveel.Events;

// Example using Entity Framework Core
public class OrderOutboxRepository : IOutboxMessageRepository<OrderOutboxMessage>
{
    private readonly AppDbContext _db;

    public OrderOutboxRepository(AppDbContext db) => _db = db;

    // ── IRepository<OrderOutboxMessage, string> ──────────────────────

    public string GetEntityKey(OrderOutboxMessage entity) => entity.Id;

    public async Task AddAsync(OrderOutboxMessage entity, CancellationToken ct = default)
    {
        await _db.OutboxMessages.AddAsync(entity, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<bool> UpdateAsync(OrderOutboxMessage entity, CancellationToken ct = default)
    {
        _db.OutboxMessages.Update(entity);
        return await _db.SaveChangesAsync(ct) > 0;
    }

    public async Task<bool> RemoveAsync(OrderOutboxMessage entity, CancellationToken ct = default)
    {
        _db.OutboxMessages.Remove(entity);
        return await _db.SaveChangesAsync(ct) > 0;
    }

    public async Task<OrderOutboxMessage?> FindAsync(string key, CancellationToken ct = default)
        => await _db.OutboxMessages.FindAsync(new object[] { key }, ct);

    // ── IOutboxMessageRepository<OrderOutboxMessage> ─────────────────

    public Task<IReadOnlyList<OrderOutboxMessage>> GetPendingMessagesAsync(
        int? limit = null, CancellationToken ct = default)
    {
        var query = _db.OutboxMessages
            .Where(m => m.Status == OutboxMessageStatus.Pending
                     && (m.NextRetryAt == null || m.NextRetryAt <= DateTimeOffset.UtcNow));

        if (limit.HasValue)
            query = query.Take(limit.Value);

        return Task.FromResult<IReadOnlyList<OrderOutboxMessage>>(query.ToList());
    }

    public async Task SetSendingAsync(OrderOutboxMessage msg, CancellationToken ct = default)
    {
        msg.Status = OutboxMessageStatus.Sending;
        await _db.SaveChangesAsync(ct);
    }

    public async Task SetDeliveredAsync(OrderOutboxMessage msg, CancellationToken ct = default)
    {
        msg.Status = OutboxMessageStatus.Delivered;
        await _db.SaveChangesAsync(ct);
    }

    public async Task SetRetryAsync(
        OrderOutboxMessage msg, string error, DateTimeOffset nextRetry, CancellationToken ct = default)
    {
        msg.Status       = OutboxMessageStatus.Pending;
        msg.ErrorMessage = error;
        msg.RetryCount  += 1;
        msg.NextRetryAt  = nextRetry;
        await _db.SaveChangesAsync(ct);
    }

    public async Task SetFailedAsync(OrderOutboxMessage msg, string error, CancellationToken ct = default)
    {
        msg.Status       = OutboxMessageStatus.Failed;
        msg.ErrorMessage = error;
        await _db.SaveChangesAsync(ct);
    }

    // ── Remaining IRepository members (AddRangeAsync / RemoveRangeAsync) ──

    public async Task AddRangeAsync(IEnumerable<OrderOutboxMessage> entities, CancellationToken ct = default)
    {
        await _db.OutboxMessages.AddRangeAsync(entities, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task RemoveRangeAsync(IEnumerable<OrderOutboxMessage> entities, CancellationToken ct = default)
    {
        _db.OutboxMessages.RemoveRange(entities);
        await _db.SaveChangesAsync(ct);
    }
}
```

> **Tip:** If you use an ORM that tracks changes automatically (EF Core, NHibernate), you can skip calling `SaveChangesAsync` inside each method and let the unit-of-work (e.g., the HTTP request scope) flush the changes.  The example above calls `SaveChangesAsync` explicitly only for clarity.

## Registration

Wire everything up in `Program.cs` (or your `Startup.cs`) using the fluent API:

### Basic registration

```csharp
using Deveel.Events;

builder.Services
    .AddEventPublisher()
    .AddOutbox<OrderOutboxMessage>()
    .WithRepository<OrderOutboxRepository>()
    .WithFactory<OrderOutboxMessageFactory>();
```

### Inline options

```csharp
builder.Services
    .AddEventPublisher()
    .AddOutbox<OrderOutboxMessage>(options =>
    {
        // OutboxPublishOptions inherits from EventPublishOptions –
        // set channel-name, content-type, etc. here if needed.
    })
    .WithRepository<OrderOutboxRepository>()
    .WithFactory<OrderOutboxMessageFactory>();
```

### From `appsettings.json`

```csharp
builder.Services
    .AddEventPublisher()
    .AddOutbox<OrderOutboxMessage>("Events:Outbox")
    .WithRepository<OrderOutboxRepository>()
    .WithFactory<OrderOutboxMessageFactory>();
```

```json
// appsettings.json
{
  "Events": {
    "Outbox": {
      "ChannelName": "outbox"
    }
  }
}
```

## Options reference

### `OutboxPublishOptions`

Inherits from `EventPublishOptions`.  Currently carries no outbox-specific properties; it exists so that future versions can add settings without a breaking change, and so that callers can pass per-call overrides through the standard options mechanism.

### `OutboxRelayOptions`

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Interval` | `TimeSpan` | `00:00:30` | How often the relay polls for pending messages |
| `MaxBatchSize` | `int` | `0` | Maximum messages processed per tick (0 = unlimited) |
| `TransportPublisherName` | `string` | `""` | Name of the publisher pipeline the relay uses to forward messages. Empty string targets the default pipeline |

## The Relay service

The relay is an `IHostedService` (`OutboxRelayService<TMessage>`) that wakes up on a configurable interval, fetches `Pending` messages from the repository, and publishes their `CloudEvent` payloads through the configured transport channels.

### Same-process deployment

In a same-process deployment the application writes to the outbox **and** runs the relay in the same host.  The relay uses `OutboxRelayPublishOptions` as a skip signal: any `OutboxPublishChannel<TMessage>` in the pipeline detects this signal and skips re-persisting the event, preventing infinite loops.

```
┌────────────────────────────────────────────────────────┐
│  App host                                              │
│                                                        │
│  ┌─────────────┐  write    ┌───────────────────────┐   │
│  │  Publisher  │ ────────► │  OutboxPublishChannel │   │
│  └─────────────┘           └──────────┬────────────┘   │
│                                       │ persists       │
│                             ┌─────────▼────────┐       │
│                             │  Outbox table    │       │
│                             └─────────┬────────┘       │
│                                       │ polls          │
│  ┌────────────────────────────────────▼────────────┐   │
│  │  OutboxRelayService (IHostedService)            │   │
│  │  → fetches pending → publishes via transport    │   │
│  └─────────────────────────────────────────────────┘   │
└────────────────────────────────────────────────────────┘
```

Add the relay to the same builder chain using `.WithRelay()`:

```csharp
builder.Services
    .AddEventPublisher()
    .AddOutbox<OrderOutboxMessage>()
    .WithRepository<OrderOutboxRepository>()
    .WithFactory<OrderOutboxMessageFactory>()
    .WithRelay(relay =>
    {
        relay.Interval     = TimeSpan.FromSeconds(15);
        relay.MaxBatchSize = 100;
    })
    // Also register the real transport to deliver to
    .AddRabbitMq(opts =>
    {
        opts.ConnectionString = "amqp://guest:guest@localhost:5672";
        opts.ExchangeName     = "events";
    });
```

Or bind relay options from configuration:

```csharp
builder.Services
    .AddEventPublisher()
    .AddOutbox<OrderOutboxMessage>("Events:Outbox")
    .WithRepository<OrderOutboxRepository>()
    .WithFactory<OrderOutboxMessageFactory>()
    .WithRelay("Events:OutboxRelay")
    .AddRabbitMq("Events:RabbitMq");
```

```json
{
  "Events": {
    "OutboxRelay": {
      "Interval": "00:00:15",
      "MaxBatchSize": 100,
      "TransportPublisherName": ""
    },
    "RabbitMq": {
      "ConnectionString": "amqp://guest:guest@localhost:5672",
      "ExchangeName": "events"
    }
  }
}
```

### Cross-process deployment

In a cross-process deployment a **separate** relay application reads the shared outbox store and delivers messages.  The relay app does **not** register the `OutboxPublishChannel` — it only needs the transport channel and the repository:

```csharp
// Relay worker (separate host / console app)
builder.Services
    .AddEventPublisher()
    .AddOutbox<OrderOutboxMessage>()
    .WithRepository<OrderOutboxRepository>()   // shared DB context
    .WithFactory<OrderOutboxMessageFactory>()
    .WithRelay(relay =>
    {
        relay.Interval     = TimeSpan.FromSeconds(10);
        relay.MaxBatchSize = 200;
    })
    .AddRabbitMq(opts =>
    {
        opts.ConnectionString = "amqp://guest:guest@rabbitmq:5672";
        opts.ExchangeName     = "events";
    });
```

In this configuration:

- The main application only has the `OutboxPublishChannel` registered (no transport).
- The relay worker has both the `OutboxRelayService` and the transport channel (no `OutboxPublishChannel`).

## Message lifecycle

```
              Publish call
                  │
                  ▼
            ┌──────────┐
            │ Pending  │ ◄─────────────────────────────┐
            └────┬─────┘                               │
                 │ relay picks up                      │
                 ▼                                     │
            ┌──────────┐                               │  retry scheduled
            │ Sending  │                               │
            └────┬─────┘                               │
        ┌────────┴────────┐                            │
   success           transient error        permanent error (max retries)
        │                 │                            │
        ▼                 └────────────────────────────┘
  ┌───────────┐                                        │
  │ Delivered │                                   ┌────▼────┐
  └───────────┘                                   │ Failed  │
                                                  └─────────┘
```

The relay processor handles the state transitions:

1. `GetPendingMessagesAsync` – fetch eligible messages.
2. `SetSendingAsync` – claim the message (prevents concurrent relay instances from double-delivering).
3. Publish via transport channel.
4. On success → `SetDeliveredAsync`.
5. On transient error → `SetRetryAsync` (with back-off timestamp).
6. When `RetryCount` exceeds the configured limit → `SetFailedAsync`.

## Complete end-to-end example

The following example shows an order service that atomically saves an order and writes an outbox record, with an in-process relay that forwards the event to RabbitMQ.

### Domain models

```csharp
using Deveel.Events;

// Event data class
[Event("order.placed", "1.0")]
public class OrderPlaced
{
    public Guid   OrderId    { get; set; }
    public string CustomerId { get; set; } = default!;
    public decimal Total     { get; set; }
}
```

### Outbox entity

```csharp
using CloudNative.CloudEvents;
using Deveel.Events;

public class OrderOutboxMessage : IOutboxMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public CloudEvent CloudEvent { get; set; } = default!;
    public OutboxMessageStatus Status { get; set; } = OutboxMessageStatus.Pending;
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public DateTimeOffset? NextRetryAt { get; set; }
}
```

### Factory

```csharp
using CloudNative.CloudEvents;
using Deveel.Events;

public class OrderOutboxMessageFactory : IOutboxMessageFactory<OrderOutboxMessage>
{
    public OrderOutboxMessage Create(CloudEvent cloudEvent, OutboxPublishOptions? options = null)
        => new() { CloudEvent = cloudEvent };
}
```

### Repository (Entity Framework Core)

```csharp
using Deveel.Events;
using Microsoft.EntityFrameworkCore;

public class AppDbContext : DbContext
{
    public DbSet<OrderOutboxMessage> OutboxMessages => Set<OrderOutboxMessage>();
    // ... other DbSets
}

public class EfOrderOutboxRepository : IOutboxMessageRepository<OrderOutboxMessage>
{
    private readonly AppDbContext _db;
    public EfOrderOutboxRepository(AppDbContext db) => _db = db;

    public string GetEntityKey(OrderOutboxMessage e) => e.Id;

    public async Task AddAsync(OrderOutboxMessage e, CancellationToken ct = default)
    { await _db.OutboxMessages.AddAsync(e, ct); await _db.SaveChangesAsync(ct); }

    public async Task<bool> UpdateAsync(OrderOutboxMessage e, CancellationToken ct = default)
    { _db.Update(e); return await _db.SaveChangesAsync(ct) > 0; }

    public async Task<bool> RemoveAsync(OrderOutboxMessage e, CancellationToken ct = default)
    { _db.Remove(e); return await _db.SaveChangesAsync(ct) > 0; }

    public Task AddRangeAsync(IEnumerable<OrderOutboxMessage> entities, CancellationToken ct = default)
    { _db.OutboxMessages.AddRange(entities); return _db.SaveChangesAsync(ct); }

    public Task RemoveRangeAsync(IEnumerable<OrderOutboxMessage> entities, CancellationToken ct = default)
    { _db.OutboxMessages.RemoveRange(entities); return _db.SaveChangesAsync(ct); }

    public async Task<OrderOutboxMessage?> FindAsync(string key, CancellationToken ct = default)
        => await _db.OutboxMessages.FindAsync(new object[] { key }, ct);

    public Task<IReadOnlyList<OrderOutboxMessage>> GetPendingMessagesAsync(
        int? limit = null, CancellationToken ct = default)
    {
        IQueryable<OrderOutboxMessage> q = _db.OutboxMessages
            .Where(m => m.Status == OutboxMessageStatus.Pending
                     && (m.NextRetryAt == null || m.NextRetryAt <= DateTimeOffset.UtcNow));
        if (limit.HasValue) q = q.Take(limit.Value);
        return Task.FromResult<IReadOnlyList<OrderOutboxMessage>>(q.ToList());
    }

    public Task SetSendingAsync(OrderOutboxMessage m, CancellationToken ct = default)
    { m.Status = OutboxMessageStatus.Sending; return _db.SaveChangesAsync(ct); }

    public Task SetDeliveredAsync(OrderOutboxMessage m, CancellationToken ct = default)
    { m.Status = OutboxMessageStatus.Delivered; return _db.SaveChangesAsync(ct); }

    public Task SetRetryAsync(OrderOutboxMessage m, string err, DateTimeOffset next, CancellationToken ct = default)
    { m.Status = OutboxMessageStatus.Pending; m.ErrorMessage = err; m.RetryCount++; m.NextRetryAt = next; return _db.SaveChangesAsync(ct); }

    public Task SetFailedAsync(OrderOutboxMessage m, string err, CancellationToken ct = default)
    { m.Status = OutboxMessageStatus.Failed; m.ErrorMessage = err; return _db.SaveChangesAsync(ct); }
}
```

### Registration (`Program.cs`)

```csharp
using Deveel.Events;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(opts =>
    opts.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services
    .AddEventPublisher(opts => opts.Source = new Uri("https://orders.example.com"))
    .AddOutbox<OrderOutboxMessage>()
    .WithRepository<EfOrderOutboxRepository>()
    .WithFactory<OrderOutboxMessageFactory>()
    .WithRelay(relay =>
    {
        relay.Interval     = TimeSpan.FromSeconds(15);
        relay.MaxBatchSize = 50;
    })
    .AddRabbitMq(opts =>
    {
        opts.ConnectionString = builder.Configuration["RabbitMq:ConnectionString"]!;
        opts.ExchangeName     = "events";
    });

var app = builder.Build();
// ... configure middleware, routes
app.Run();
```

### Service layer

```csharp
public class OrderService
{
    private readonly AppDbContext    _db;
    private readonly IEventPublisher _publisher;

    public OrderService(AppDbContext db, IEventPublisher publisher)
    {
        _db        = db;
        _publisher = publisher;
    }

    public async Task PlaceOrderAsync(PlaceOrderCommand cmd, CancellationToken ct = default)
    {
        var order = new Order { CustomerId = cmd.CustomerId, Total = cmd.Total };
        await _db.Orders.AddAsync(order, ct);

        // PublishAsync writes ONLY to the outbox table (same transaction context).
        // The outbox record and the order row are saved in the same SaveChangesAsync call
        // further down, or each AddAsync flushes immediately for simplicity.
        await _publisher.PublishAsync(new OrderPlaced
        {
            OrderId    = order.Id,
            CustomerId = order.CustomerId,
            Total      = order.Total,
        }, cancellationToken: ct);
    }
}
```

## Combining Outbox with other channels

You can register the outbox channel alongside transport channels.  All registered channels receive every event.  Use [Named Channels](named-channels.md) to direct specific events to specific channels or to suppress certain channels for certain events.

```csharp
builder.Services
    .AddEventPublisher()
    // Persist every event to the outbox
    .AddOutbox<OrderOutboxMessage>()
        .WithRepository<EfOrderOutboxRepository>()
        .WithFactory<OrderOutboxMessageFactory>()
        .WithRelay(r => r.Interval = TimeSpan.FromSeconds(20))
    // Also deliver high-priority events directly via webhook
    .AddWebhooks(opts =>
    {
        opts.ChannelName  = "priority-webhook";
        opts.EndpointUrl  = "https://partner.example.com/events";
        opts.SigningSecret = "s3cr3t";
    });
```

## Related pages

- [Publisher Channels Overview](README.md)
- [Named Channels](named-channels.md)
- [Typed Channels](typed-channels.md)
- [RabbitMQ Channel](rabbitmq.md)
- [Azure Service Bus Channel](azure-service-bus.md)

