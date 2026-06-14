# NDJSON Backend for Audit Trail

The NDJSON backend writes audit entries as newline-delimited JSON to rolling files, providing a simple, efficient, and compliance-ready storage solution.

## How It Works

Each audit entry is serialized as a single JSON line and appended to the current file:

```json
{"id":"evt-123","eventType":"order.placed","source":"https://orders.example.com","subject":"order/456","timestamp":"2026-06-14T10:30:12.451Z","data":{"orderId":"456","customerId":"cust-789","total":99.95},"storedAt":"2026-06-14T10:30:12.453Z"}
{"id":"evt-124","eventType":"order.confirmed","source":"https://orders.example.com","subject":"order/456","timestamp":"2026-06-14T10:30:15.789Z","data":{"orderId":"456","confirmedAt":"2026-06-14T10:30:15.780Z"},"storedAt":"2026-06-14T10:30:15.792Z"}
```

## File Management

### File Naming

Files are named with a sortable, sequence-numbered pattern:

```
audit-trail/
  audit-trail-20260614-103012-000001.ndjson
  audit-trail-20260614-103017-000002.ndjson
  audit-trail-20260614-103024-000003.ndjson
```

Format: `audit-trail-{yyyyMMdd-HHmmss}-{sequence}.ndjson`

Multiple rolls within the same second produce distinct files via the sequence number.

### Auto-Roll

The backend rolls to a new file when either condition is met:

1. **Size-based roll** — Current file exceeds `MaxFileSizeBytes`
2. **Time-based roll** — `RollInterval` has elapsed

### Cleanup

After each write, the backend:
1. Counts files in the directory
2. Deletes the oldest files beyond `MaxFileCount`
3. Set `MaxFileCount <= 0` to disable cleanup (for compliance scenarios)

## Configuration

### Basic Setup

```csharp
var auditDir = Path.Combine(AppContext.BaseDirectory, "audit-trail");
Directory.CreateDirectory(auditDir);

builder.Services.AddEventPublisher(o =>
{
    o.Source = new Uri("https://orders.example.com");
})
.AddAuditTrail(audit => audit.UseNDJson(options =>
{
    options.DirectoryPath = auditDir;
    options.MaxFileSizeBytes = 10 * 1024 * 1024;  // 10 MB
    options.RollInterval = TimeSpan.FromMinutes(5);
    options.MaxFileCount = 20;
}));
```

### Options Reference

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `DirectoryPath` | `string` | `%TEMP%/audit-trail` | Directory for NDJSON files |
| `MaxFileSizeBytes` | `long` | 10 MB | Size threshold for rolling |
| `RollInterval` | `TimeSpan?` | `null` | Time threshold for rolling (`null` = disabled) |
| `MaxFileCount` | `int` | `30` | Max files retained (`≤0` = no cleanup) |

### Production Settings (Compliance)

```csharp
options.DirectoryPath = "/secure/audit/hermodr";
options.MaxFileSizeBytes = 50 * 1024 * 1024;  // 50 MB
options.RollInterval = TimeSpan.FromHours(12);
options.MaxFileCount = 730;  // Keep ~1 year at 2 rolls/day
```

## Performance Characteristics

### Writes

- **Serialized** through semaphores to prevent file corruption
- **Async I/O** — non-blocking writes
- **Buffered** — efficient sequential writes
- **Low latency** — typically < 1ms per write

### Reads

- **Stream-based** — files opened with `FileShare.ReadWrite`
- **Line-by-line** — `StreamReader.ReadLineAsync`, never loads full file into memory
- **Concurrent** — external readers can access files while writer is active
- **Filtering** — queries applied during streaming (before loading into memory)

## Pluggable Filesystem

All I/O goes through the `IFileSystem` abstraction from [`System.IO.Abstractions`](https://www.nuget.org/packages/System.IO.Abstractions):

```csharp
// Register custom filesystem (e.g., Azure Blob Storage)
builder.Services.AddSingleton<IFileSystem>(sp => 
    new MyAzureBlobFileSystem(connectionString));

builder.Services.AddEventPublisher()
    .AddAuditTrail(audit => audit.UseNDJson(o => 
        o.DirectoryPath = "audit-trail"));
```

The `NdJsonAuditTrail` does not touch `System.IO` directly — every call goes through `IFileSystem`, so any compatible adapter works without changes.

### Supported Filesystem Adapters

| Adapter | Package | Use Case |
|---------|---------|----------|
| **PhysicalFileSystem** | `System.IO.Abstractions` | Local disk (default) |
| **Azure Blob FileSystem** | `System.IO.Abstractions.Wrappers` + custom | Azure storage |
| **AWS S3 FileSystem** | Custom implementation | S3-compatible storage |
| **MockFileSystem** | `System.IO.Abstractions.TestingHelpers` | Unit testing |

## Querying NDJSON Files

Use the `IAuditTrailReader` for stream-based queries:

### Register Reader

The reader is registered independently so a query API can live in a different process:

```csharp
builder.Services.AddNDJsonAuditTrailQuerying(options =>
{
    options.DirectoryPath = auditDir;
});
```

### Query Examples

```csharp
var reader = provider.GetRequiredService<IAuditTrailReader<AuditTrailEntry>>();

// Query by event type
var query = new AuditTrailStreamQuery
{
    EventType = "order.placed"
};

var entries = new List<AuditTrailEntry>();
await foreach (var entry in reader.ReadAsync(query))
{
    entries.Add(entry);
}

// Query by time range
var timeRangeQuery = new AuditTrailStreamQuery
{
    From = DateTimeOffset.UtcNow.AddHours(-1),
    To = DateTimeOffset.UtcNow
};

// Query by subject
var subjectQuery = new AuditTrailStreamQuery
{
    Subject = "order/456"
};

// Combined query with limit
var combinedQuery = new AuditTrailStreamQuery
{
    EventType = "order.*",
    Subject = "order/456",
    From = DateTimeOffset.UtcNow.AddDays(-7),
    Limit = 100
};
```

### Stream Query Options

| Option | Type | Description |
|--------|------|-------------|
| `EventType` | `string?` | Filter by event type (wildcards supported) |
| `Source` | `string?` | Filter by event source |
| `Subject` | `string?` | Filter by event subject |
| `From` | `DateTimeOffset?` | Filter entries after this time |
| `To` | `DateTimeOffset?` | Filter entries before this time |
| `Limit` | `int?` | Maximum entries to return |

## Monitoring NDJSON Files

### Tail Current File (Linux/macOS)

```bash
tail -f /secure/audit/hermodr/audit-trail-*.ndjson
```

### Count Entries

```bash
wc -l /secure/audit/hermodr/audit-trail-*.ndjson
```

### Find Specific Events

```bash
grep '"eventType":"order.placed"' /secure/audit/hermodr/*.ndjson | head -100
```

### Parse with jq

```bash
cat /secure/audit/hermodr/*.ndjson | \
  jq 'select(.eventType == "order.placed") | {id, timestamp, data}'
```

## Compliance Features

### Immutability

- Files are **append-only** — once written, entries cannot be modified
- File permissions should be set to read-only after rolling
- Consider WORM (Write Once, Read Many) storage for strict compliance

### Retention

- Set `MaxFileCount = 0` to disable automatic cleanup
- Implement manual archival process for old files
- Move archived files to cold storage (e.g., Azure Archive, AWS Glacier)

### Encryption

- Enable disk encryption at the OS level
- Or use encrypted filesystem adapters (Azure Storage with encryption at rest)
- Consider encrypting individual entries before writing

### Access Logging

Log all access to audit trail files:

```csharp
public class AuditedAuditTrailReader : IAuditTrailReader<AuditTrailEntry>
{
    private readonly IAuditTrailReader<AuditTrailEntry> _inner;
    private readonly ILogger<AuditedAuditTrailReader> _logger;

    public async IAsyncEnumerable<AuditTrailEntry> ReadAsync(
        AuditTrailStreamQuery query, [EnumeratorCancellation] CancellationToken ct = default)
    {
        _logger.LogInformation("Audit trail accessed by {User} with query {Query}", 
            _currentUser, query);

        await foreach (var entry in _inner.ReadAsync(query, ct))
        {
            yield return entry;
        }
    }
}
```

## Related Pages

- [Audit Trail Overview](README.md)
- [Querying](querying.md)
- [Filesystem Abstraction](filesystem-abstraction.md)
