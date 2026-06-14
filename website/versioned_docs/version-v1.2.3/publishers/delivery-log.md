# Publish Delivery Log

The `Deveel.Events.Publisher.DeliveryLog` package records operational telemetry for every event publish attempt: which channel was used, when the attempt happened, how many times it was retried, how long it took, whether it succeeded or failed, and what error occurred. The records are stored in a pluggable storage backend of your choice.

## Why use the Delivery Log?

The existing [Dead-Letter Channel](dead-letter.md) captures only failed events and preserves them for replay. The Event Store (planned) records domain facts — the event payloads themselves — for auditing and read-model rebuilding. Neither answers operational questions about the *publishing infrastructure*:

- How many times did we attempt to send event X before it succeeded?
- Which channel is producing the most failures?
- What was the average delivery latency last week?
- Did a specific subscriber receive all events during an outage window?

The Delivery Log fills this gap. It records structured, queryable telemetry about every delivery attempt — successes, failures, retries — so you can monitor publish health, compute SLAs, and debug delivery issues without relying on broker-specific dashboards or generic application logs.

## Architecture

The Delivery Log is implemented as a middleware that sits in the event publisher's pipeline, plus an optional error handler for the pipeline's failure path.

```
IEventPublisher
    │
    ▼
[Middleware Pipeline]
    │
    ├── DeliveryLogMiddleware
    │     captures start time → calls next → writes outcome + elapsed
    │
    ▼
Channel publish
    │
    ├── success ──► record Succeeded
    │
    └── failure ──► middleware records Failed (re-throws exception)
                    │
                    └── DeliveryLogPublishErrorHandler (optional)
                         records failure for ThrowOnErrors=false path
```

## Core types

The feature is built on six types, each with a focused responsibility:

**`IEventPublishDeliveryLog`** — the write-only surface. It exposes a single method `RecordAsync(IEventDeliveryRecord, CancellationToken)`. The middleware depends on this interface, which keeps it decoupled from query capabilities.

**`IEventDeliveryRecord`** — the read-only data contract for one delivery attempt. It carries the event itself, publisher metadata, attempt number, timestamp, outcome, error details, and elapsed time. 

**`EventDeliveryOutcome`** — three-state enum: `Succeeded` (delivered without exception), `Failed` (terminal failure), `Retried` (failure with retry scheduled; reserved for future retry infrastructure).

**`IEventDeliveryLogRepository<TRecord>`** — the read/write repository. It extends the write interface and adds four query methods: `GetByEventIdAsync`, `GetByChannelAsync`, `GetByOutcomeAsync`, `GetByTimeRangeAsync`. It also extends `IRepository<TRecord>` from `Deveel.Repository.Core` for standard CRUD operations (though NDJSON backend throws for mutation). A non-generic alias `IEventDeliveryLogRepository` is provided, defaulting to `EventDeliveryRecord`.

## How it works

### Delivery Log Middleware

The middleware is an `IEventMiddleware` registered via `EventPublisherBuilder.Use<T>()`. On every publish call it:

1. Captures `IEventSystemTime.UtcNow` as the start time. Using the system time abstraction (rather than direct `DateTimeOffset.UtcNow`) makes timestamps deterministic in tests — you can inject a frozen clock.
2. Reads the current attempt number from `EventContext`. On first call it initializes the counter to 2 (for the next attempt) and returns 1. Subsequent calls return and increment the existing value.
3. Executes the rest of the pipeline via `next(context)`.
4. On success: the outcome is set to `Succeeded`.
5. On exception: the outcome is set to `Failed`, the exception type name and message are captured as `ErrorCode` and `ErrorMessage`, and the exception is **re-thrown** — the middleware never swallows publish failures.
6. In a `finally` block: constructs an `EventDeliveryRecord` with all captured values and writes it to `IEventPublishDeliveryLog.RecordAsync()`. If the store write itself fails, the exception is logged and **swallowed** — a storage backend failure never cascades into a publish failure.

### Delivery Log Error Handler

The error handler implements `IEventPublishErrorHandler` and is registered via `DeliveryLogBuilder.UseErrorHandler()`. It serves the pipeline's error path rather than the middleware path:

- It only acts when `context.Stage == EventPublishStage.ChannelPublish` and `context.Event` is non-null.
- It writes a record with `Outcome = Failed`, the exception type name and message from the error context.
- `ElapsedTime` is set to `TimeSpan.Zero` (the exact start time is not available in the error context).

Use the error handler when `ThrowOnErrors = false` and you still want failures recorded. Use both middleware and error handler when you want every attempt captured (middleware covers the attempt itself, the error handler covers the error pipeline processing).

## Installation

The core package depends on `Deveel.Events.Publisher` (for the middleware pipeline), `Deveel.Repository.Core` and `Deveel.Repository.InMemory` (for storage abstractions), and `Microsoft.Extensions.Logging.Abstractions`.

```bash
dotnet add package Deveel.Events.Publisher.DeliveryLog
```

For EF Core persistence (depends on `Deveel.Repository.EntityFramework`):

```bash
dotnet add package Deveel.Events.Publisher.DeliveryLog.EntityFramework
dotnet add package Microsoft.EntityFrameworkCore.Sqlite
```

Replace `Sqlite` with your actual provider (`SqlServer`, `Npgsql`, etc.).

## Registration

### Via EventPublisherBuilder

`AddDeliveryLog()` is an extension on `EventPublisherBuilder`. It registers the in-memory store as default and adds the middleware to the pipeline.

```csharp
builder.Services
    .AddEventPublisher(options =>
        options.Source = new Uri("https://orders.example.com"))
    .AddDeliveryLog();
```

With a storage backend:

```csharp
.AddDeliveryLog(log => log.UseNDJson())
```

With the error handler:

```csharp
.AddDeliveryLog(log => log.UseErrorHandler())
```

With a custom store:

```csharp
.AddDeliveryLog(log => log.UseStore<MyCustomStore>())
.AddDeliveryLog(log => log.UseStore(myInstance))
```

Only one storage backend is active — subsequent `Use*` calls replace the previous registration.

### Standalone (without publisher)

If you only need the repository as a data store independent of the publisher pipeline:

```csharp
services.AddDeliveryLog(log => log.UseNDJson(opts =>
{
    opts.DirectoryPath = "/var/logs/events";
    opts.MaxFileSizeBytes = 50 * 1024 * 1024;
}));
```

This registers the repository without a middleware pipeline.

## Storage backends

Each backend implements both `IEventPublishDeliveryLog` and `IEventDeliveryLogRepository`.

### In-Memory (default)

`InMemoryEventDeliveryLogRepository` extends `InMemoryRepository<EventDeliveryRecord>`. All records are held in a thread-safe, volatile collection. Registered as Singleton. Suitable for tests and local development, but records are lost on process restart.

```csharp
.AddDeliveryLog(log => log.UseInMemory())
```

### NDJSON rolling files

The NDJSON implementation of the repository appends each record as a JSON line to a sequentially-named file. Files are named `delivery-log-{yyyyMMdd-HHmmss}.ndjson` in a configurable directory.

The repository auto-rolls to a new file when either the current file exceeds `MaxFileSizeBytes` (default 10 MB) or the `RollInterval` has elapsed. After each write, it checks the file count and deletes the oldest files beyond `MaxFileCount` (default 30). Set `MaxFileCount <= 0` to disable cleanup.

- Write are serialized through semaphores. 
- Files are opened with a read/write sharing so external readers can tail them concurrently. 
- Records are serialized as camelCase JSON using a `CloudEventJsonConverter` that encodes the CloudEvent in structured mode.

The NDJSON backend is append-only - `Update`, `Remove`, `AddRange`, `RemoveRange`, and `Find` throw `NotSupportedException`. 

**Note** - Query methods scan all files linearly, so performance degrades with data volume.

```csharp
.AddDeliveryLog(log => log.UseNDJson(opts =>
{
    opts.DirectoryPath = "/var/logs/delivery-logs";
    opts.MaxFileSizeBytes = 10 * 1024 * 1024;
    opts.RollInterval = TimeSpan.FromHours(6);
    opts.MaxFileCount = 30;
}))
```

| Option | Type | Default | Description |
|---|---|---|---|
| `DirectoryPath` | `string` | `%TEMP%/delivery-logs` | Directory for NDJSON files |
| `MaxFileSizeBytes` | `long` | 10 MB | Size threshold for rolling |
| `RollInterval` | `TimeSpan?` | `null` | Time threshold for rolling (null = disabled) |
| `MaxFileCount` | `int` | `30` | Max files retained (≤0 = no cleanup) |

### Entity Framework Core

`EntityEventDeliveryLogRepository` extends `EntityRepository<DbEventDeliveryRecord, string>` and stores records in a relational database. It supports the full `IRepository` contract including CRUD operations, with all four query methods translated to LINQ expressions.

```csharp
.AddDeliveryLog(log => log.UseEntityFramework(opts =>
    opts.UseSqlite("Data Source=delivery-log.db")))
```

The `DbEventDeliveryRecord` entity maps to the `delivery_records` table with the following schema:

| Column | Type | Constraints |
|---|---|---|
| `Id` | `string(256)` | Primary key |
| `EventId` | `string(256)` | Required, indexed |
| `EventType` | `string(256)` | Nullable |
| `EventData` | string (JSON) | Nullable |
| `PublisherName` | `string(256)` | Nullable |
| `ChannelName` | `string(256)` | Nullable, indexed |
| `ChannelType` | `string(256)` | Nullable |
| `AttemptNumber` | `int` | Required, default 1 |
| `Timestamp` | `DateTimeOffset` | Required, UTC, indexed |
| `Outcome` | `string(32)` | Required, indexed |
| `ErrorCode` | `string(128)` | Nullable |
| `ErrorMessage` | string | Nullable |
| `ElapsedTimeTicks` | `long` | Required, default 0 |

The `EventData` column stores the CloudEvent as structured-mode JSON using the `CloudNative.CloudEvents` SDK, enabling full event rehydration on reads. The `Outcome` enum is stored as a string. The `ElapsedTime` is stored as ticks.

Create the database schema via `DeliveryLogDbContext.Database.EnsureCreatedAsync()` or EF Core migrations for production schema management.

## Querying delivery records

All four query methods return `IReadOnlyList<EventDeliveryRecord>` ordered by timestamp: ascending for event ID and time range queries (chronological history), descending for channel and outcome queries (most recent first).

```csharp
Task<IReadOnlyList<EventDeliveryRecord>> GetByEventIdAsync(string eventId, CancellationToken ct = default);
Task<IReadOnlyList<EventDeliveryRecord>> GetByChannelAsync(string channelName, CancellationToken ct = default);
Task<IReadOnlyList<EventDeliveryRecord>> GetByOutcomeAsync(EventDeliveryOutcome outcome, CancellationToken ct = default);
Task<IReadOnlyList<EventDeliveryRecord>> GetByTimeRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);
```

Inject `IEventDeliveryLogRepository` into any service for reporting, SLA monitoring, or diagnostics. The returned records include the full CloudEvent (on backends that support it), so you can inspect event metadata alongside delivery telemetry without a separate lookup.

## Relation to other features

| Feature | Scope | Primary use case |
|---|---|---|
| **Delivery Log** | Attempt metadata per publish | Operational visibility into publish health |
| **Dead-Letter** | Failed event payloads + replay | Recovering from delivery failures |
| **Error Handling** | Pipeline error interception | Custom error policies (logging, circuit-breaker) |
| **Event Store** (planned) | Domain fact audit trail | Compliance, read-model rebuilding |
| **OpenTelemetry** (planned) | Trace context propagation | End-to-end distributed tracing |

The Delivery Log and Dead-Letter are complementary: the log records *that* delivery failed and how long it took; the dead-letter preserves *what* failed so you can replay it.

## Related pages

- [Publisher Channels Overview](README.md)
- [Publish Error Handling](error-handling.md)
- [Dead-Letter Handling and Replay](dead-letter.md)
- [Transactional Outbox](outbox.md)
