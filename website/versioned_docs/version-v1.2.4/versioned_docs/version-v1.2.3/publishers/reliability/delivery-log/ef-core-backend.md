# EF Core Backend

The Entity Framework Core backend stores delivery records in a relational database.

## What's Included

| Type | Purpose |
|------|---------|
| `DeliveryLogDbContext` | EF Core `DbContext` with delivery log entity configured |
| `DbDeliveryLogEntry` | Entity mapping for `EventDeliveryRecord` |
| `EntityDeliveryLogRepository` | Repository implementation |

## Registration

### Basic Setup

```csharp
builder.Services
    .AddEventPublisher()
    .AddDeliveryLog(log => log.UseEntityFramework(opts =>
        opts.UseSqlServer(connectionString)));
```

### With Custom DbContext

```csharp
public class AppDbContext : DeliveryLogDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    
    public DbSet<Order> Orders { get; set; } = null!;
}

builder.Services.AddDbContext<AppDbContext>(opts =>
    opts.UseSqlServer(connectionString));

builder.Services
    .AddEventPublisher()
    .AddDeliveryLog(log => log.UseEntityFramework<AppDbContext>());
```

## Database Schema

### DeliveryLogEntries Table

```sql
CREATE TABLE DeliveryLogEntries (
    Id              nvarchar(450) NOT NULL PRIMARY KEY,
    EventId         nvarchar(256) NULL,
    EventType       nvarchar(256) NOT NULL,
    Source          nvarchar(256) NOT NULL,
    Subject         nvarchar(256) NULL,
    EventTime       datetimeoffset NOT NULL,
    DataContentType nvarchar(256) NULL,
    PublisherName   nvarchar(256) NOT NULL,
    ChannelType     nvarchar(256) NOT NULL,
    ChannelName     nvarchar(256) NULL,
    Attempt         int NOT NULL,
    Timestamp       datetimeoffset NOT NULL,
    Outcome         int NOT NULL,
    ErrorCode       nvarchar(256) NULL,
    ErrorMessage    nvarchar(max) NULL,
    ElapsedTime     bigint NOT NULL,  -- ticks
    DataText        nvarchar(max) NULL,
    DataBytes       varbinary(max) NULL
);
```

## Querying

### Basic Queries

```csharp
using var db = provider.GetRequiredService<DeliveryLogDbContext>();

// Count all records
var totalCount = await db.DeliveryLogEntries.CountAsync();

// Count failures in last hour
var failureCount = await db.DeliveryLogEntries
    .CountAsync(e => e.Outcome == EventDeliveryOutcome.Failed 
                  && e.Timestamp > DateTimeOffset.UtcNow.AddHours(-1));

// Get recent failures
var failures = await db.DeliveryLogEntries
    .Where(e => e.Outcome == EventDeliveryOutcome.Failed)
    .OrderByDescending(e => e.Timestamp)
    .Take(100)
    .ToListAsync();
```

### Aggregation Queries

```csharp
// Average latency by event type (last hour)
var avgLatency = await db.DeliveryLogEntries
    .Where(e => e.Timestamp > DateTimeOffset.UtcNow.AddHours(-1))
    .GroupBy(e => e.EventType)
    .Select(g => new 
    {
        EventType = g.Key,
        AvgLatencyMs = g.Average(e => e.ElapsedTime.TotalMilliseconds),
        Count = g.Count()
    })
    .OrderByDescending(x => x.Count)
    .ToListAsync();

// Success rate by channel
var successRates = await db.DeliveryLogEntries
    .Where(e => e.Timestamp > DateTimeOffset.UtcNow.AddHours(-24))
    .GroupBy(e => e.ChannelType)
    .Select(g => new
    {
        ChannelType = g.Key,
        TotalCount = g.Count(),
        SuccessCount = g.Count(e => e.Outcome == EventDeliveryOutcome.Succeeded),
        SuccessRate = (double)g.Count(e => e.Outcome == EventDeliveryOutcome.Succeeded) / g.Count() * 100
    })
    .ToListAsync();
```

### Time-Series Queries

```csharp
// Failures per hour (last 24 hours)
var failuresPerHour = await db.DeliveryLogEntries
    .Where(e => e.Outcome == EventDeliveryOutcome.Failed 
             && e.Timestamp > DateTimeOffset.UtcNow.AddHours(-24))
    .GroupBy(e => new { e.Timestamp.Hour, e.Timestamp.Date })
    .Select(g => new
    {
        Hour = g.Key.Date.AddHours(g.Key.Hour),
        Count = g.Count()
    })
    .OrderBy(x => x.Hour)
    .ToListAsync();
```

## Performance Considerations

### Indexing

For better query performance, consider adding indexes:

```csharp
modelBuilder.Entity<DbDeliveryLogEntry>(entity =>
{
    entity.HasIndex(e => new { e.Timestamp, e.Outcome });
    entity.HasIndex(e => e.EventType);
    entity.HasIndex(e => e.ChannelType);
});
```

### Retention Policy

Implement a retention policy to prevent unbounded growth:

```csharp
// Delete records older than 30 days
var cutoffDate = DateTimeOffset.UtcNow.AddDays(-30);
var deletedCount = await db.DeliveryLogEntries
    .Where(e => e.Timestamp < cutoffDate)
    .ExecuteDeleteAsync();
```

### Archival Strategy

For compliance scenarios, archive old records before deletion:

```csharp
// Archive to separate table
var cutoffDate = DateTimeOffset.UtcNow.AddDays(-90);
var oldRecords = await db.DeliveryLogEntries
    .Where(e => e.Timestamp < cutoffDate)
    .ToListAsync();

// Insert into archive table
await db.DeliveryLogArchive.AddRangeAsync(oldRecords);
await db.SaveChangesAsync();

// Delete from main table
db.DeliveryLogEntries.RemoveRange(oldRecords);
await db.SaveChangesAsync();
```

## Monitoring Dashboard Queries

### Key Metrics

```csharp
// Current queue depth (pending deliveries from last 5 minutes)
var pendingCount = await db.DeliveryLogEntries
    .CountAsync(e => e.Timestamp > DateTimeOffset.UtcNow.AddMinutes(-5) 
                  && e.Outcome == EventDeliveryOutcome.Failed);

// P95 latency (last hour)
var p95Latency = await db.DeliveryLogEntries
    .Where(e => e.Timestamp > DateTimeOffset.UtcNow.AddHours(-1))
    .OrderBy(e => e.ElapsedTime.TotalMilliseconds)
    .Skip((int)(await db.DeliveryLogEntries.CountAsync(e => 
        e.Timestamp > DateTimeOffset.UtcNow.AddHours(-1))) * 0.95)
    .Select(e => e.ElapsedTime.TotalMilliseconds)
    .FirstOrDefaultAsync();

// Error rate (last hour)
var total = await db.DeliveryLogEntries.CountAsync(e => 
    e.Timestamp > DateTimeOffset.UtcNow.AddHours(-1));
var failed = await db.DeliveryLogEntries.CountAsync(e => 
    e.Outcome == EventDeliveryOutcome.Failed && 
    e.Timestamp > DateTimeOffset.UtcNow.AddHours(-1));
var errorRate = (double)failed / total * 100;
```

## Related Pages

- [Delivery Log Overview](README.md)
- [Storage Backends](storage-backends.md)
- [NDJSON Backend](ndjson-backend.md)
