# Publish Delivery Log

The `Hermodr.Publisher.DeliveryLog` package records operational telemetry for every event publish attempt: which channel was used, when the attempt happened, how many times it was retried, how long it took, whether it succeeded or failed, and what error occurred. The records are stored in a pluggable storage backend of your choice.

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

The feature is built on three types, each with a focused responsibility:

**`IEventPublishDeliveryLog`** — the write-only surface. It exposes a single method `RecordAsync(EventDeliveryRecord, CancellationToken)`. The middleware depends on this interface, which keeps it decoupled from query capabilities.

**`EventDeliveryRecord`** — the concrete data contract for one delivery attempt. It carries the event itself, publisher metadata, attempt number, timestamp, outcome, error details, and elapsed time.

**`EventDeliveryOutcome`** — three-state enum: `Succeeded` (delivered without exception), `Failed` (terminal failure), `Retried` (failure with retry scheduled; reserved for future retry infrastructure).

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

The core package depends on `Hermodr.Publisher` (for the middleware pipeline) and `Microsoft.Extensions.Logging.Abstractions`.

```bash
dotnet add package Hermodr.Publisher.DeliveryLog
```

For in-memory storage (recommended for tests and local development):

```bash
dotnet add package Hermodr.Publisher.DeliveryLog.InMemory
```

For NDJSON rolling-file storage:

```bash
dotnet add package Hermodr.Publisher.DeliveryLog.NDJson
```

For EF Core persistence:

```bash
dotnet add package Hermodr.Publisher.DeliveryLog.EntityFramework
dotnet add package Microsoft.EntityFrameworkCore.Sqlite
```

Replace `Sqlite` with your actual provider (`SqlServer`, `Npgsql`, etc.).

## Registration

### Via EventPublisherBuilder

`AddDeliveryLog()` is an extension on `EventPublisherBuilder`. It adds the delivery log middleware to the pipeline.

```csharp
builder.Services
    .AddEventPublisher(options =>
        options.Source = new Uri("https://orders.example.com"))
    .AddDeliveryLog(log => log.UseInMemory());
```

With the NDJSON backend (requires `Hermodr.Publisher.DeliveryLog.NDJson`):

```csharp
.AddDeliveryLog(log => log.UseNDJson())
```

With EF Core (requires `Hermodr.Publisher.DeliveryLog.EntityFramework`):

```csharp
.AddDeliveryLog(log => log.UseEntityFramework(opts =>
    opts.UseSqlite("Data Source=delivery-log.db")))
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

If you only need the delivery log services independent of the publisher pipeline:

```csharp
services.AddDeliveryLog(log => log.UseNDJson(opts =>
{
    opts.DirectoryPath = "/var/logs/events";
    opts.MaxFileSizeBytes = 50 * 1024 * 1024;
}));
```

This registers the `IEventPublishDeliveryLog` service without a middleware pipeline.

## Storage backends

Each backend implements `IEventPublishDeliveryLog`. The storage packages are distributed separately and must be installed explicitly. Only one storage backend is active — subsequent `Use*` calls replace the previous registration.

### In-Memory (requires `Hermodr.Publisher.DeliveryLog.InMemory`)

`InMemoryEventDeliveryLogRepository` holds all records in a thread-safe, volatile collection. Registered as Singleton. Suitable for tests and local development, but records are lost on process restart.

```csharp
.AddDeliveryLog(log => log.UseInMemory())
```

### NDJSON rolling files (requires `Hermodr.Publisher.DeliveryLog.NDJson`)

The NDJSON implementation appends each record as a JSON line to a sequentially-named file. Files are named `delivery-log-{yyyyMMdd-HHmmss}.ndjson` in a configurable directory.

The backend auto-rolls to a new file when either the current file exceeds `MaxFileSizeBytes` (default 10 MB) or the `RollInterval` has elapsed. After each write, it checks the file count and deletes the oldest files beyond `MaxFileCount` (default 30). Set `MaxFileCount <= 0` to disable cleanup.

- Writes are serialized through semaphores.
- Files are opened with read/write sharing so external readers can tail them concurrently.
- Records are serialized as camelCase JSON using a `CloudEventJsonConverter` that encodes the CloudEvent in structured mode.

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

### Entity Framework Core (requires `Hermodr.Publisher.DeliveryLog.EntityFramework`)

Stores records in a relational database using `Kista.EntityFramework`.

```csharp
.AddDeliveryLog(log => log.UseEntityFramework(opts =>
    opts.UseSqlite("Data Source=delivery-log.db")))
```

## Relation to other features

| Feature | Scope | Primary use case |
|---|---|---|
| **Delivery Log** | Attempt metadata per publish | Operational visibility into publish health |
| **Dead-Letter** | Failed event payloads + replay | Recovering from delivery failures |
| **Error Handling** | Pipeline error interception | Custom error policies (logging, circuit-breaker) |
| **Audit Trail** | Domain fact audit trail | Compliance, read-model rebuilding |
| **OpenTelemetry** | Trace context propagation | End-to-end distributed tracing |

The Delivery Log and Dead-Letter are complementary: the log records *that* delivery failed and how long it took; the dead-letter preserves *what* failed so you can replay it.

## Related pages

- [Publisher Channels Overview](README.md)
- [Publish Error Handling](error-handling.md)
- [Dead-Letter Handling and Replay](dead-letter.md)
- [Transactional Outbox](outbox.md)
