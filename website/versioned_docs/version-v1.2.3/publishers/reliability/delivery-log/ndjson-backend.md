# NDJSON Backend

The NDJSON backend writes delivery records as newline-delimited JSON to rolling files.

## How It Works

Each delivery record is serialized as a single JSON line and appended to the current file:

```json
{"id":"abc123","eventType":"order.placed","source":"https://orders.example.com","timestamp":"2026-06-14T10:30:12.451Z","publisherName":"default","channelType":"RabbitMqPublishChannel","attempt":1,"outcome":"Succeeded","elapsedTime":"00:00:00.1234567"}
{"id":"def456","eventType":"order.placed","source":"https://orders.example.com","timestamp":"2026-06-14T10:30:13.789Z","publisherName":"default","channelType":"RabbitMqPublishChannel","attempt":1,"outcome":"Failed","errorCode":"RabbitMQ.Client.Exceptions.BrokerUnreachableException","errorMessage":"None of the specified endpoints were reachable","elapsedTime":"00:00:05.0001234"}
```

## File Management

### File Naming

Files are named with a sortable, timestamp-based pattern:

```
delivery-log-20260614-103012.ndjson
delivery-log-20260614-103045.ndjson
delivery-log-20260614-104500.ndjson
```

Format: `delivery-log-{yyyyMMdd-HHmmss}.ndjson`

### Auto-Roll

The backend rolls to a new file when either condition is met:

1. **Size-based roll** — Current file exceeds `MaxFileSizeBytes` (default 10 MB)
2. **Time-based roll** — `RollInterval` has elapsed

### Cleanup

After each write, the backend:
1. Counts files in the directory
2. Deletes the oldest files beyond `MaxFileCount` (default 30)
3. Set `MaxFileCount <= 0` to disable cleanup

## Configuration

### Basic Setup

```csharp
builder.Services
    .AddEventPublisher()
    .AddDeliveryLog(log => log.UseNDJson(opts =>
    {
        opts.DirectoryPath = "/var/logs/delivery-logs";
        opts.MaxFileSizeBytes = 10 * 1024 * 1024;  // 10 MB
        opts.RollInterval = TimeSpan.FromHours(6);
        opts.MaxFileCount = 30;
    }));
```

### Options Reference

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `DirectoryPath` | `string` | `%TEMP%/delivery-logs` | Directory for NDJSON files |
| `MaxFileSizeBytes` | `long` | 10 MB | Size threshold for rolling |
| `RollInterval` | `TimeSpan?` | `null` | Time threshold for rolling (`null` = disabled) |
| `MaxFileCount` | `int` | `30` | Max files retained (`≤0` = no cleanup) |

### Recommended Production Settings

```csharp
opts.DirectoryPath = "/var/logs/hermodr/delivery-logs";
opts.MaxFileSizeBytes = 50 * 1024 * 1024;  // 50 MB
opts.RollInterval = TimeSpan.FromHours(12);
opts.MaxFileCount = 60;  // Keep ~30 days at 2 rolls/day
```

## Performance Characteristics

### Writes

- **Serialized** through semaphores to prevent file corruption
- **Async I/O** — non-blocking writes
- **Buffered** — efficient sequential writes

### Reads

- **Stream-based** — files opened with `FileShare.ReadWrite`
- **Line-by-line** — `StreamReader.ReadLineAsync`, never loads full file into memory
- **Concurrent** — external readers can tail files while writer is active

## Serialization

Records are serialized as camelCase JSON using a `CloudEventJsonConverter` that encodes the CloudEvent in structured mode:

```csharp
{
  "id": "abc123",
  "eventType": "order.placed",
  "source": "https://orders.example.com",
  "timestamp": "2026-06-14T10:30:12.451Z",
  "publisherName": "default",
  "channelType": "RabbitMqPublishChannel",
  "attempt": 1,
  "outcome": "Succeeded",
  "elapsedTime": "00:00:00.1234567",
  "event": {
    "specversion": "1.0",
    "type": "order.placed",
    "source": "https://orders.example.com",
    "id": "evt-123",
    "time": "2026-06-14T10:30:12.451Z",
    "data": { ... }
  }
}
```

## Querying NDJSON Files

Use the `NdJsonDeliveryLogReader` for stream-based queries:

```csharp
var reader = provider.GetRequiredService<NdJsonDeliveryLogReader>();

var query = new DeliveryLogStreamQuery
{
    From = DateTimeOffset.UtcNow.AddHours(-1),
    To = DateTimeOffset.UtcNow,
    Outcome = EventDeliveryOutcome.Failed
};

await foreach (var record in reader.ReadAsync(query))
{
    Console.WriteLine($"Failed: {record.Event.Type} - {record.ErrorMessage}");
}
```

### Stream Query Options

| Option | Type | Description |
|--------|------|-------------|
| `From` | `DateTimeOffset?` | Filter records after this time |
| `To` | `DateTimeOffset?` | Filter records before this time |
| `EventType` | `string?` | Filter by event type |
| `Outcome` | `EventDeliveryOutcome?` | Filter by outcome |
| `PublisherName` | `string?` | Filter by publisher name |
| `Limit` | `int?` | Maximum records to return |

## Monitoring NDJSON Files

### Tail Current File (Linux/macOS)

```bash
tail -f /var/logs/delivery-logs/delivery-log-*.ndjson
```

### Count Records in File

```bash
wc -l /var/logs/delivery-logs/delivery-log-20260614-*.ndjson
```

### Find Failures

```bash
grep '"outcome":"Failed"' /var/logs/delivery-logs/*.ndjson | tail -100
```

### Parse with jq

```bash
cat /var/logs/delivery-logs/*.ndjson | \
  jq 'select(.outcome == "Failed") | {eventType, errorCode, errorMessage}'
```

## Related Pages

- [Delivery Log Overview](README.md)
- [Storage Backends](storage-backends.md)
- [EF Core Backend](ef-core-backend.md)
