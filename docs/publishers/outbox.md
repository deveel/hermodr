# Transactional Outbox Channel

The `Deveel.Events.Publisher.Outbox` package implements the **Transactional Outbox** pattern for Deveel Events.  Instead of dispatching a `CloudEvent` directly to a message broker, the publisher first persists the event into the same transactional store as the business data.  A separate relay process then picks up pending records and forwards them to the real transport channel.

## Why use the Outbox pattern?

In a distributed system, the moment between committing a domain change and dispatching a message to a broker is a window of potential data loss.  If the application crashes after the commit but before the send, the event is silently dropped.  If it crashes after the send but before the commit, the broker receives an event for a change that never happened.

The Transactional Outbox pattern closes that window in three steps:

1. **Write atomically** – the domain change and the outbox record are saved in the **same database transaction**, so they either both succeed or both roll back.
2. **Relay independently** – a background process reads confirmed outbox records and delivers their payloads to the broker.  This decouples durability from transport availability.
3. **Guarantee delivery** – if the relay crashes mid-flight it can restart and pick up exactly where it left off, because an outbox record is only removed (or marked `Delivered`) after the broker has acknowledged receipt.

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

## How it works

When `IEventPublisher.PublishAsync` is called, the outbox channel serialises the `CloudEvent` and persists it as a new record in the outbox store with status `Pending`.  The business transaction that triggered the publish can commit both the domain row and the outbox row atomically — no message has yet been sent to the broker.

The relay service wakes up on a configurable interval and queries the repository for all `Pending` records whose earliest retry time has passed.  For each record it:

1. Marks the record as `Sending` so that concurrent relay instances do not double-deliver it.
2. Forwards the `CloudEvent` payload to the configured transport channel (RabbitMQ, Azure Service Bus, etc.).
3. On success, marks the record `Delivered`.
4. On a transient error, schedules a retry and increments the retry counter.
5. When retries are exhausted, marks the record `Failed`.

## Implementing the outbox contract

To use the outbox channel you provide three components that adapt the library to your persistence technology.  The library defines the contracts; you supply the implementations.

### Outbox message entity

Your entity class must implement `IOutboxMessage`.  It acts as the database row that the relay service reads and the channel writes.  The interface requires the following properties:

| Property | Type | Purpose |
|----------|------|---------|
| `CloudEvent` | `CloudEvent` | The event payload to deliver |
| `Status` | `OutboxMessageStatus` | Tracks where the record is in the delivery lifecycle |
| `ErrorMessage` | `string?` | The last failure reason, populated when the relay encounters an error |
| `RetryCount` | `int` | How many delivery attempts have been made |
| `NextRetryAt` | `DateTimeOffset?` | Earliest time the relay may attempt delivery again; `null` means immediately eligible |

Your entity should also carry whatever primary-key and ORM-mapping attributes your data layer requires (e.g., `[Key]` for EF Core, column mappings, index attributes).

A minimal entity looks like:

```csharp
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

### Message factory

`IOutboxMessageFactory<TMessage>` has a single method, `Create`, that converts a `CloudEvent` into a new instance of your entity.  The framework calls it once per `PublishAsync` invocation.

The factory should be **stateless** and **allocation-light** — it only needs to wrap the provided event and set `Status = Pending`.  

```csharp
public class OrderOutboxMessageFactory : IOutboxMessageFactory<OrderOutboxMessage>
{
    public OrderOutboxMessage Create(CloudEvent cloudEvent, OutboxPublishOptions? options = null)
        => new() { CloudEvent = cloudEvent };
}
```

The `options` parameter carries any per-call publish options (channel name, content-type overrides, etc.) in case you need to store them alongside the entity.

### Repository

`IOutboxMessageRepository<TMessage>` extends the standard `IRepository<TMessage, string>` CRUD surface with outbox-specific state-transition methods.  The CRUD methods (`AddAsync`, `UpdateAsync`, `RemoveAsync`, `FindAsync`, `AddRangeAsync`, `RemoveRangeAsync`) are straightforward persistence operations that you implement using your ORM or data-access library of choice.

The outbox-specific methods handle the delivery lifecycle:

| Method | When called | What to do |
|--------|-------------|------------|
| `GetPendingMessagesAsync(limit?, ct)` | Every relay tick | Return `Pending` records whose `NextRetryAt` is `null` or in the past; apply `limit` if provided |
| `SetSendingAsync(msg, ct)` | Before each delivery attempt | Set `Status = Sending` so concurrent relay instances skip this record |
| `SetDeliveredAsync(msg, ct)` | After successful delivery | Set `Status = Delivered`; the record may now be archived or deleted |
| `SetRetryAsync(msg, error, nextRetryAt, ct)` | After a transient failure | Set `Status = Pending`, record the error, increment `RetryCount`, set `NextRetryAt` |
| `SetFailedAsync(msg, error, ct)` | When retry limit is exceeded | Set `Status = Failed`; human intervention or a dead-letter process is required |

> **Atomicity tip:** the `AddAsync` method is called from within the same `DbContext` scope as the domain entity write, so for EF Core you should **not** call `SaveChangesAsync` inside it — let the caller flush the unit of work.  The state-transition methods (`SetSendingAsync`, etc.) are called by the relay service outside of a business transaction, so they should persist immediately.

For Entity Framework Core the outbox-specific methods typically query the `DbSet` with a `Where` clause on `Status` and `NextRetryAt`, update the entity's properties, then call `SaveChangesAsync`:

```csharp
public class EfOrderOutboxRepository : IOutboxMessageRepository<OrderOutboxMessage>
{
    private readonly AppDbContext _db;
    public EfOrderOutboxRepository(AppDbContext db) => _db = db;

    // --- IRepository<OrderOutboxMessage, string> members ---
    // Implement AddAsync, UpdateAsync, RemoveAsync, FindAsync,
    // AddRangeAsync, and RemoveRangeAsync using _db.OutboxMessages.

    // --- Outbox-specific members ---

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

## Registration

Once your three components are ready, wire them up in `Program.cs` using the fluent `EventPublisherBuilder` chain.  The `.AddOutbox<TMessage>()` call registers the `OutboxPublishChannel`, and the subsequent `.WithRepository<T>()` and `.WithFactory<T>()` calls bind your implementations.

### Minimal setup

```csharp
builder.Services
    .AddEventPublisher()
    .AddOutbox<OrderOutboxMessage>()
    .WithRepository<EfOrderOutboxRepository>()
    .WithFactory<OrderOutboxMessageFactory>();
```

### With inline options

```csharp
builder.Services
    .AddEventPublisher()
    .AddOutbox<OrderOutboxMessage>(options =>
    {
        options.ChannelName = "outbox";
    })
    .WithRepository<EfOrderOutboxRepository>()
    .WithFactory<OrderOutboxMessageFactory>();
```

### Bound from configuration

```csharp
builder.Services
    .AddEventPublisher()
    .AddOutbox<OrderOutboxMessage>("Events:Outbox")
    .WithRepository<EfOrderOutboxRepository>()
    .WithFactory<OrderOutboxMessageFactory>();
```

```json
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

Inherits from `EventPublishOptions` and currently adds no outbox-specific properties.  It exists as a dedicated type so that future releases can introduce outbox-only settings without a breaking change, and so that callers can provide per-publish overrides through the standard options path.

### `OutboxRelayOptions`

Controls the background relay service.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Interval` | `TimeSpan` | `00:00:30` | How often the relay polls the repository for pending messages |
| `MaxBatchSize` | `int` | `0` | Maximum number of messages the relay processes per poll cycle; `0` means no limit |
| `TransportPublisherName` | `string` | `""` | Name of the downstream publisher pipeline used to forward messages. Leave empty to target the default pipeline |

## The relay service

The relay is an `IHostedService` that the framework registers automatically when you call `.WithRelay(…)`.  It runs in the background on the configured `Interval`, fetches a batch of `Pending` messages, and publishes each one's `CloudEvent` to the transport channel.

### Same-process deployment

The simplest topology runs the relay inside the same application host as the publisher.  The relay detects that it is running in the same process and automatically skips the outbox channel during forwarding, so events are not re-persisted in an infinite loop.

Add `.WithRelay()` to the builder chain and then register the target transport:

```csharp
builder.Services
    .AddEventPublisher()
    .AddOutbox<OrderOutboxMessage>()
    .WithRepository<EfOrderOutboxRepository>()
    .WithFactory<OrderOutboxMessageFactory>()
    .WithRelay(relay =>
    {
        relay.Interval     = TimeSpan.FromSeconds(15);
        relay.MaxBatchSize = 100;
    })
    .AddRabbitMq(opts =>
    {
        opts.ConnectionString = "amqp://guest:guest@localhost:5672";
        opts.ExchangeName     = "events";
    });
```

Configuration-bound equivalent:

```csharp
builder.Services
    .AddEventPublisher()
    .AddOutbox<OrderOutboxMessage>("Events:Outbox")
    .WithRepository<EfOrderOutboxRepository>()
    .WithFactory<OrderOutboxMessageFactory>()
    .WithRelay("Events:OutboxRelay")
    .AddRabbitMq("Events:RabbitMq");
```

```json
{
  "Events": {
    "OutboxRelay": { "Interval": "00:00:15", "MaxBatchSize": 100 },
    "RabbitMq":    { "ConnectionString": "amqp://...", "ExchangeName": "events" }
  }
}
```

### Cross-process deployment

For larger deployments it is common to run the relay as a **dedicated worker process** that shares the database with the main application but runs independently.  This allows the relay to be scaled, deployed, and restarted without affecting the application.

In this topology:

- The **main application** registers only `AddOutbox<TMessage>()` (no relay, no transport channel).  Its sole job is to persist outbox records atomically with domain data.
- The **relay worker** registers the repository, the factory, `.WithRelay(…)`, and the transport channel — but **not** the `OutboxPublishChannel` itself.

```csharp
// Relay worker Program.cs
builder.Services
    .AddEventPublisher()
    .AddOutbox<OrderOutboxMessage>()
    .WithRepository<EfOrderOutboxRepository>()  // points at the shared database
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

## Message lifecycle

Each outbox record moves through a well-defined set of states that reflect its position in the delivery pipeline:

| Status | Meaning |
|--------|---------|
| `Pending` | The record has been written and is waiting to be picked up by the relay |
| `Sending` | The relay has claimed the record and is attempting delivery |
| `Delivered` | The transport channel has acknowledged receipt; the record can be archived or removed |
| `Failed` | All delivery attempts have been exhausted; manual intervention is needed |

```
              Publish call
                  │
                  ▼
            ┌──────────┐
            │ Pending  │ ◄──────────────────────────────┐
            └────┬─────┘                                │
                 │ relay claims                         │
                 ▼                                      │
            ┌──────────┐                                │  back-off & retry
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

## End-to-end example

The following walkthrough shows a minimal order service that atomically writes an order row and an outbox record, then relies on the in-process relay to forward the event to RabbitMQ.

### Event data class

Annotate the event data class with `[Event]` so that the framework can generate the correct CloudEvents `type` and `dataschemaversion` attributes automatically:

```csharp
[Event("order.placed", "1.0")]
public class OrderPlaced
{
    public Guid    OrderId    { get; set; }
    public string  CustomerId { get; set; } = default!;
    public decimal Total      { get; set; }
}
```

### Host registration

Wire all components in `Program.cs`:

```csharp
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
```

### Publishing from a service

Inject `IEventPublisher` and call `PublishAsync` as usual.  No special outbox API is required — the channel selection is transparent:

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

        // Writes only to the outbox table; the relay will forward to RabbitMQ.
        await _publisher.PublishAsync(new OrderPlaced
        {
            OrderId    = order.Id,
            CustomerId = order.CustomerId,
            Total      = order.Total,
        }, cancellationToken: ct);

        await _db.SaveChangesAsync(ct);  // commits both the order row and the outbox record
    }
}
```

Notice that `SaveChangesAsync` is called **once** after both the domain write and the publish call, ensuring that the two rows commit atomically.

## Combining Outbox with other channels

The outbox channel participates in the same fan-out as any other channel — every event published to the `IEventPublisher` is delivered to all registered channels.  You can therefore mix the outbox with a direct transport channel to get different delivery guarantees for different scenarios.

Use [Named Channels](named-channels.md) to route specific event types to specific channels or to exclude a channel for a particular publish call.

```csharp
builder.Services
    .AddEventPublisher()
    .AddOutbox<OrderOutboxMessage>()           // guaranteed, async delivery for all events
        .WithRepository<EfOrderOutboxRepository>()
        .WithFactory<OrderOutboxMessageFactory>()
        .WithRelay(r => r.Interval = TimeSpan.FromSeconds(20))
    .AddWebhooks(opts =>                       // direct delivery for high-priority events
    {
        opts.ChannelName   = "priority-webhook";
        opts.EndpointUrl   = "https://partner.example.com/events";
        opts.SigningSecret = "s3cr3t";
    });
```

## Related pages

- [Publisher Channels Overview](README.md)
- [Named Channels](named-channels.md)
- [Typed Channels](typed-channels.md)
- [RabbitMQ Channel](rabbitmq.md)
- [Azure Service Bus Channel](azure-service-bus.md)

