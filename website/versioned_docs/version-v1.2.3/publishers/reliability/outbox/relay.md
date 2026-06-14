# Outbox Relay Service

The relay is a background service that polls the outbox table for pending messages and forwards them to the actual transport channel.

## How the Relay Works

The relay is an `IHostedService` registered automatically when you call `.WithRelay(…)`. It runs in a continuous loop:

```
┌─────────────────────────────────────────────────────────────┐
│                    Relay Service Loop                       │
│                                                             │
│   ┌─────────────┐                                          │
│   │   Sleep     │ ◄────────────────────────────┐           │
│   │  (Interval) │                              │           │
│   └──────┬──────┘                              │           │
│          │                                     │           │
│          ▼                                     │           │
│   ┌─────────────────┐                          │           │
│   │ Query Pending   │ Get messages where       │           │
│   │   Messages      │ Status = Pending AND     │           │
│   └──────┬──────┘   │ NextRetryAt <= Now       │           │
│          │           └──────────────────────────┘           │
│          ▼                                     ▲           │
│   ┌─────────────────┐                          │           │
│   │  For each msg:  │                          │           │
│   │  - Set Sending  │                          │           │
│   │  - Publish      │                          │           │
│   │  - On success:  │                          │           │
│   │      Delivered  │                          │           │
│   │  - On error:    │                          │           │
│   │      Retry/Fail │                          │           │
│   └─────────────────┘                          │           │
│                                                 │           │
│   Continue until MaxBatchSize reached ──────────┘           │
└─────────────────────────────────────────────────────────────┘
```

## Registration

### With Inline Configuration

```csharp
builder.Services
    .AddEventPublisher()
    .AddOutbox<OrderOutboxMessage>()
    .WithEntityFramework(opts => opts.UseSqlServer(connectionString))
    .WithFactory<OrderOutboxMessageFactory>()
    .WithRelay(relay =>
    {
        relay.Interval     = TimeSpan.FromSeconds(15);
        relay.MaxBatchSize = 100;
    });
```

### From Configuration

```csharp
builder.Services
    .AddEventPublisher()
    .AddOutbox<OrderOutboxMessage>()
    .WithEntityFramework(opts => opts.UseSqlServer(connectionString))
    .WithFactory<OrderOutboxMessageFactory>()
    .WithRelay("Events:OutboxRelay");
```

```json
{
  "Events": {
    "OutboxRelay": {
      "Interval": "00:00:15",
      "MaxBatchSize": 100
    }
  }
}
```

---

## OutboxRelayOptions

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Interval` | `TimeSpan` | `00:00:30` | How often the relay polls for pending messages |
| `MaxBatchSize` | `int` | `0` | Maximum messages per poll cycle (0 = no limit) |
| `TransportPublisherName` | `string` | `""` | Name of downstream publisher pipeline for forwarding |

### Interval

The `Interval` controls how frequently the relay wakes up to check for pending messages:

- **Shorter interval** (e.g., 5 seconds) → Lower latency, more database queries
- **Longer interval** (e.g., 60 seconds) → Higher latency, fewer database queries

**Recommendation:** Start with 15-30 seconds for most applications. Adjust based on:
- Acceptable delivery latency
- Database load tolerance
- Expected message volume

### MaxBatchSize

Controls how many messages the relay processes per poll cycle:

- **`0` or negative** → No limit (processes all pending messages)
- **Positive value** → Limits batch size to prevent long-running cycles

**Recommendation:** Set a reasonable limit (50-200) to:
- Prevent the relay from monopolizing resources
- Ensure the relay completes within the interval
- Allow graceful shutdown between batches

### TransportPublisherName

Specifies which publisher pipeline the relay uses to forward messages:

- **Empty string (default)** → Uses the default publisher pipeline
- **Named pipeline** → Targets a specific publisher configuration

This is useful when you want to separate the outbox persistence from the transport configuration.

---

## Deployment Topologies

### Same-Process Deployment

The relay runs inside the same application host as the publisher:

```csharp
builder.Services
    .AddEventPublisher()
    .AddOutbox<OrderOutboxMessage>()
    .WithEntityFramework(opts => opts.UseSqlServer(connectionString))
    .WithFactory<OrderOutboxMessageFactory>()
    .WithRelay()  // Relay runs in this process
    .AddRabbitMq(opts => { /* transport config */ });
```

**Pros:**
- Simple setup — one deployment
- No additional infrastructure
- Good for development and small-scale applications

**Cons:**
- Relay competes for resources with the main application
- Relay restarts when the application restarts
- Cannot scale relay independently

**When to use:**
- Development and testing
- Low to medium message volumes
- Applications where simplicity is paramount

---

### Cross-Process Deployment

The relay runs as a **dedicated worker process** separate from the main application:

#### Main Application (Publisher Only)

```csharp
// Main application Program.cs
builder.Services
    .AddEventPublisher()
    .AddOutbox<OrderOutboxMessage>()
    .WithEntityFramework(opts => 
        opts.UseSqlServer(builder.Configuration.GetConnectionString("Shared")));
    // No relay, no transport channel registered here
```

#### Relay Worker (Separate Process)

```csharp
// Relay worker Program.cs
builder.Services
    .AddEventPublisher()
    .AddOutbox<OrderOutboxMessage>()
    .WithEntityFramework(opts =>
        opts.UseSqlServer(builder.Configuration.GetConnectionString("Shared")))
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

**Pros:**
- Independent scaling of relay workers
- Isolated failures — relay crashes don't affect the main application
- Dedicated resources for relay processing
- Can deploy relay updates without redeploying the main application

**Cons:**
- More complex deployment (separate worker process)
- Requires shared database access
- Additional infrastructure to manage

**When to use:**
- High message volumes
- Mission-critical applications requiring high availability
- When relay processing is resource-intensive
- When you need to scale relay independently

---

## Relay Behavior

### Message Claiming

When the relay picks up a pending message, it sets `Status = Sending` before attempting delivery. This prevents:

- **Duplicate delivery** — Concurrent relay instances won't process the same message
- **Race conditions** — Clear ownership during the delivery attempt

### Retry Logic

On transient failures, the relay:

1. Increments `RetryCount`
2. Sets `NextRetryAt` based on a back-off strategy
3. Sets `Status = Pending` to make the message eligible for future attempts

On permanent failures (retry limit exceeded):

1. Sets `Status = Failed`
2. Records the final `ErrorMessage`
3. Requires manual intervention or dead-letter processing

### Graceful Shutdown

The relay respects `CancellationToken` and will:

1. Stop polling for new messages
2. Complete processing of in-flight messages (if time permits)
3. Shut down cleanly without losing message state

---

## Monitoring the Relay

### Key Metrics to Track

| Metric | Description | Alert Threshold |
|--------|-------------|-----------------|
| `PendingMessageCount` | Number of messages waiting to be delivered | > 1000 for > 5 minutes |
| `FailedMessageCount` | Number of messages in Failed status | Any failures |
| `AverageDeliveryLatency` | Time from EventTime to Delivered status | > 5 minutes |
| `RelayCycleDuration` | How long each poll cycle takes | > Interval |
| `RetryRate` | Percentage of messages requiring retry | > 10% |

### Querying Outbox Status

```csharp
// Using EF Core
var pendingCount = await _db.OutboxMessages
    .CountAsync(m => m.Status == OutboxMessageStatus.Pending);

var failedMessages = await _db.OutboxMessages
    .Where(m => m.Status == OutboxMessageStatus.Failed)
    .OrderByDescending(m => m.LastStatusAt)
    .Take(100)
    .ToListAsync();

var averageLatency = await _db.OutboxMessages
    .Where(m => m.Status == OutboxMessageStatus.Delivered 
             && m.EventTime > DateTimeOffset.UtcNow.AddHours(-1))
    .AverageAsync(m => (m.LastStatusAt - m.CreatedAt).TotalSeconds);
```

---

## Related Pages

- [Transactional Outbox Overview](README.md)
- [Registration](registration.md)
- [Lifecycle](lifecycle.md)
