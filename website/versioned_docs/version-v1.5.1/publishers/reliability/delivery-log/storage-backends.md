# Delivery Log Storage Backends

Each backend implements `IEventPublishDeliveryLog`. Storage packages are distributed separately and must be installed explicitly.

> **Note:** Only one storage backend is active — subsequent `Use*` calls replace the previous registration.

## In-Memory Storage

**Package:** `Hermodr.Publisher.DeliveryLog.InMemory`

`InMemoryEventDeliveryLogRepository` holds all records in a thread-safe, volatile collection. Registered as Singleton.

### Characteristics

| Aspect | Description |
|--------|-------------|
| **Persistence** | None — records lost on process restart |
| **Performance** | Fastest — no I/O overhead |
| **Query capability** | In-memory LINQ queries |
| **Best for** | Development, testing, local debugging |

### Registration

```csharp
builder.Services
    .AddEventPublisher()
    .AddDeliveryLog(log => log.UseInMemory());
```

### Querying

```csharp
var log = provider.GetRequiredService<InMemoryEventDeliveryLogRepository>();
var records = log.Records.Where(r => r.Outcome == EventDeliveryOutcome.Failed);
```

---

## NDJSON Rolling Files

**Package:** `Hermodr.Publisher.DeliveryLog.NDJson`

The NDJSON implementation appends each record as a JSON line to a sequentially-named file.

### Characteristics

| Aspect | Description |
|--------|-------------|
| **Persistence** | File-based — survives process restarts |
| **Performance** | Good — sequential writes, async I/O |
| **Query capability** | Stream-based reading, filtering on read |
| **Best for** | Production, audit scenarios, external processing |

### File Naming

Files are named `delivery-log-{yyyyMMdd-HHmmss}.ndjson` in a configurable directory:

```
/var/logs/delivery-logs/
  delivery-log-20260614-103012.ndjson
  delivery-log-20260614-103045.ndjson
  delivery-log-20260614-104500.ndjson
```

### Auto-Roll Behavior

The backend auto-rolls to a new file when either:
- Current file exceeds `MaxFileSizeBytes` (default 10 MB)
- `RollInterval` has elapsed

After each write, it checks the file count and deletes the oldest files beyond `MaxFileCount` (default 30).

### Registration

```csharp
builder.Services
    .AddEventPublisher()
    .AddDeliveryLog(log => log.UseNDJson(opts =>
    {
        opts.DirectoryPath = "/var/logs/delivery-logs";
        opts.MaxFileSizeBytes = 10 * 1024 * 1024;
        opts.RollInterval = TimeSpan.FromHours(6);
        opts.MaxFileCount = 30;
    }));
```

### Options Reference

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `DirectoryPath` | `string` | `%TEMP%/delivery-logs` | Directory for NDJSON files |
| `MaxFileSizeBytes` | `long` | 10 MB | Size threshold for rolling |
| `RollInterval` | `TimeSpan?` | `null` | Time threshold for rolling |
| `MaxFileCount` | `int` | `30` | Max files retained (≤0 = no cleanup) |

### Reading NDJSON Files

```csharp
var query = new DeliveryLogStreamQuery
{
    From = DateTimeOffset.UtcNow.AddHours(-1),
    Outcome = EventDeliveryOutcome.Failed
};

await foreach (var record in reader.ReadAsync(query))
{
    Console.WriteLine($"Failed: {record.Event.Type} - {record.ErrorMessage}");
}
```

---

## Entity Framework Core

**Package:** `Hermodr.Publisher.DeliveryLog.EntityFramework`

Stores records in a relational database using Kista.EntityFramework.

### Characteristics

| Aspect | Description |
|--------|-------------|
| **Persistence** | Database — durable, transactional |
| **Performance** | Moderate — depends on database |
| **Query capability** | Full LINQ, indexes, aggregations |
| **Best for** | Production, monitoring dashboards, reporting |

### Registration

```csharp
builder.Services
    .AddEventPublisher()
    .AddDeliveryLog(log => log.UseEntityFramework(opts =>
        opts.UseSqlServer(connectionString)));
```

### Database Schema

The EF Core backend creates a `DeliveryLogEntries` table with columns for all `EventDeliveryRecord` fields.

### Querying

```csharp
using var db = provider.GetRequiredService<DeliveryLogDbContext>();

// Count failures in last hour
var failureCount = await db.DeliveryLogEntries
    .CountAsync(e => e.Outcome == EventDeliveryOutcome.Failed 
                  && e.Timestamp > DateTimeOffset.UtcNow.AddHours(-1));

// Average latency by event type
var avgLatency = await db.DeliveryLogEntries
    .Where(e => e.EventTime > DateTimeOffset.UtcNow.AddHours(-1))
    .GroupBy(e => e.Event.Type)
    .Select(g => new 
    {
        EventType = g.Key,
        AvgLatencyMs = g.Average(e => e.ElapsedTime.TotalMilliseconds)
    })
    .ToListAsync();
```

---

## Backend Comparison

| Backend | Persistence | Performance | Query Capability | Best For |
|---------|-------------|-------------|------------------|----------|
| **In-Memory** | None | Fastest | LINQ | Development/Testing |
| **NDJSON** | Files | Good | Stream-based | Production, audit |
| **EF Core** | Database | Moderate | Full LINQ | Production, reporting |

---

## Related Pages

- [Delivery Log Overview](README.md)
- [NDJSON Backend](ndjson-backend.md)
- [EF Core Backend](ef-core-backend.md)
