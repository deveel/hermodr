# Querying the Audit Trail

The audit trail provides stream-based querying capabilities that are efficient, non-blocking, and scalable.

## IAuditTrailReader

The read interface for audit trail entries:

```csharp
public interface IAuditTrailReader<TEntry>
{
    IAsyncEnumerable<TEntry> ReadAsync(
        AuditTrailStreamQuery query,
        CancellationToken cancellationToken = default);
}
```

## Registration

### NDJSON Backend

The reader is registered independently from the writer:

```csharp
builder.Services.AddNDJsonAuditTrailQuerying(options =>
{
    options.DirectoryPath = auditDir;
});
```

This allows the reader and writer to:
- Live in separate processes
- Scale independently
- Use different configurations

### EF Core Backend

```csharp
builder.Services.AddAuditTrailQuerying(opts =>
    opts.UseSqlServer(connectionString));
```

## AuditTrailStreamQuery

The query object for filtering audit entries:

```csharp
public class AuditTrailStreamQuery
{
    public string? EventType { get; set; }      // Filter by event type (wildcards supported)
    public string? Source { get; set; }         // Filter by event source
    public string? Subject { get; set; }        // Filter by event subject
    public DateTimeOffset? From { get; set; }   // Filter entries after this time
    public DateTimeOffset? To { get; set; }     // Filter entries before this time
    public int? Limit { get; set; }             // Maximum entries to return
}
```

## Query Examples

### Basic Queries

#### All Entries

```csharp
var reader = provider.GetRequiredService<IAuditTrailReader<AuditTrailEntry>>();

await foreach (var entry in reader.ReadAsync(new AuditTrailStreamQuery()))
{
    Console.WriteLine($"{entry.Timestamp}: {entry.EventType}");
}
```

#### By Event Type

```csharp
var query = new AuditTrailStreamQuery
{
    EventType = "order.placed"
};

await foreach (var entry in reader.ReadAsync(query))
{
    Console.WriteLine($"Order placed: {entry.Data}");
}
```

#### By Time Range

```csharp
var query = new AuditTrailStreamQuery
{
    From = DateTimeOffset.UtcNow.AddHours(-1),
    To = DateTimeOffset.UtcNow
};

await foreach (var entry in reader.ReadAsync(query))
{
    Console.WriteLine($"{entry.Timestamp}: {entry.EventType}");
}
```

#### By Subject

```csharp
var query = new AuditTrailStreamQuery
{
    Subject = "order/456"
};

await foreach (var entry in reader.ReadAsync(query))
{
    Console.WriteLine($"Order 456 event: {entry.EventType}");
}
```

### Advanced Queries

#### Wildcard Event Type

```csharp
var query = new AuditTrailStreamQuery
{
    EventType = "order.*"  // Matches order.placed, order.confirmed, etc.
};
```

#### Combined Filters

```csharp
var query = new AuditTrailStreamQuery
{
    EventType = "payment.*",
    Source = "https://payments.example.com",
    From = DateTimeOffset.UtcNow.AddDays(-7),
    Limit = 100
};

var entries = new List<AuditTrailEntry>();
await foreach (var entry in reader.ReadAsync(query))
{
    entries.Add(entry);
}

Console.WriteLine($"Found {entries.Count} payment events");
```

#### With Limit

```csharp
var query = new AuditTrailStreamQuery
{
    EventType = "order.placed",
    Limit = 10  // Return at most 10 entries
};
```

## Querying in ASP.NET Core

### Minimal API Example

```csharp
app.MapGet("/api/audit-trail", async (
    IAuditTrailReader<AuditTrailEntry> reader,
    string? eventType, string? source, string? subject,
    DateTimeOffset? from, DateTimeOffset? to, int? limit) =>
{
    var query = new AuditTrailStreamQuery
    {
        EventType = eventType,
        Source = source,
        Subject = subject,
        From = from,
        To = to,
        Limit = limit ?? 100
    };

    var entries = new List<AuditTrailEntry>();
    var max = query.Limit ?? 100;
    
    await foreach (var entry in reader.ReadAsync(query))
    {
        if (entries.Count >= max) break;
        entries.Add(entry);
    }
    
    return Results.Ok(entries);
});
```

### Query by Event ID

```csharp
app.MapGet("/api/audit-trail/event/{eventId}", async (
    string eventId,
    IAuditTrailReader<AuditTrailEntry> reader) =>
{
    var query = new AuditTrailStreamQuery
    {
        Limit = 1000  // Reasonable upper bound
    };

    await foreach (var entry in reader.ReadAsync(query))
    {
        if (entry.Id == eventId)
        {
            return Results.Ok(entry);
        }
    }

    return Results.NotFound();
});
```

### Query by Event Type

```csharp
app.MapGet("/api/audit-trail/type/{eventType}", async (
    string eventType,
    IAuditTrailReader<AuditTrailEntry> reader,
    int? limit) =>
{
    var query = new AuditTrailStreamQuery
    {
        EventType = eventType,
        Limit = limit ?? 100
    };

    var entries = new List<AuditTrailEntry>();
    await foreach (var entry in reader.ReadAsync(query))
    {
        entries.Add(entry);
        if (entries.Count >= query.Limit) break;
    }

    return Results.Ok(entries);
});
```

### Aggregate Statistics

```csharp
app.MapGet("/api/audit-trail/stats", async (
    IAuditTrailReader<AuditTrailEntry> reader,
    DateTimeOffset? from, DateTimeOffset? to) =>
{
    var query = new AuditTrailStreamQuery
    {
        From = from ?? DateTimeOffset.UtcNow.AddHours(-24),
        To = to ?? DateTimeOffset.UtcNow
    };

    var eventTypeCounts = new Dictionary<string, int>();
    
    await foreach (var entry in reader.ReadAsync(query))
    {
        if (!eventTypeCounts.TryGetValue(entry.EventType, out var count))
        {
            count = 0;
        }
        eventTypeCounts[entry.EventType] = count + 1;
    }

    return Results.Ok(new
    {
        From = query.From,
        To = query.To,
        TotalEvents = eventTypeCounts.Values.Sum(),
        EventsByType = eventTypeCounts
    });
});
```

## Performance Considerations

### Streaming vs Loading

The audit trail reader uses **streaming** — entries are read one at a time, not loaded into memory:

```csharp
// ✅ GOOD: Streaming, memory-efficient
await foreach (var entry in reader.ReadAsync(query))
{
    Process(entry);
}

// ❌ BAD: Loads all entries into memory
var allEntries = await reader.ReadAsync(query).ToListAsync();
```

### Filtering Early

Apply filters in the query, not after loading:

```csharp
// ✅ GOOD: Filter in query
var query = new AuditTrailStreamQuery
{
    EventType = "order.placed",
    From = DateTimeOffset.UtcNow.AddHours(-1)
};

// ❌ BAD: Filter after loading
var allEntries = await reader.ReadAsync(new AuditTrailStreamQuery()).ToListAsync();
var filtered = allEntries
    .Where(e => e.EventType == "order.placed")
    .Where(e => e.Timestamp > DateTimeOffset.UtcNow.AddHours(-1))
    .ToList();
```

### Use Limits

Always specify a `Limit` for unbounded queries:

```csharp
// ✅ GOOD: Bounded query
var query = new AuditTrailStreamQuery
{
    EventType = "order.*",
    Limit = 1000
};

// ❌ BAD: Unbounded query could return millions of entries
var query = new AuditTrailStreamQuery
{
    EventType = "order.*"
};
```

## EF Core Querying

When using the EF Core backend, you get full LINQ support:

```csharp
using var db = provider.GetRequiredService<AuditTrailDbContext>();

// Complex query with aggregations
var stats = await db.AuditTrailEntries
    .Where(e => e.Timestamp > DateTimeOffset.UtcNow.AddHours(-24))
    .GroupBy(e => e.EventType)
    .Select(g => new
    {
        EventType = g.Key,
        Count = g.Count(),
        FirstOccurrence = g.Min(e => e.Timestamp),
        LastOccurrence = g.Max(e => e.Timestamp)
    })
    .OrderByDescending(x => x.Count)
    .ToListAsync();
```

## Related Pages

- [Audit Trail Overview](README.md)
- [NDJSON Backend](ndjson-backend.md)
- [Examples](examples.md)
