# Sample: OrderService — Audit Trail with NDJson Files

**Location:** [`samples/audit-trail-ndjson/AuditTrail.NDJson.Sample/`](https://github.com/deveel/hermodr/tree/main/samples/audit-trail-ndjson/AuditTrail.NDJson.Sample)  
**Framework:** ASP.NET Core 9 Minimal API  
**Audit Trail Backend:** `Hermodr.AuditTrail.NDJson`

---

## Overview

This sample shows how to use **Hermodr's NDJson audit trail backend** to persist every published CloudEvent to disk as newline-delimited JSON, with automatic file rolling and a pluggable filesystem abstraction.

```
POST   /api/orders  → 4 events published  → NDJson files in ./audit-trail-ndjson/
GET    /api/audit-trail?eventType=...        → filtered stream of entries
GET    /api/audit-trail/event/{eventId}      → entries for a specific CloudEvent
GET    /api/audit-trail/type/{eventType}     → entries for an event type
GET    /api/audit-trail/stats                → aggregate statistics
```

---

## What this sample demonstrates

### 1. Configuring the NDJson audit trail backend

```csharp
var auditDir = Path.Combine(AppContext.BaseDirectory, "audit-trail-ndjson");
Directory.CreateDirectory(auditDir);

builder.Services.AddEventPublisher(o =>
{
    o.Source = new Uri("https://samples.deveel.events/audit-trail-ndjson");
    o.DataSchemaBaseUri = new Uri("https://schemas.deveel.events/");
})
    .AddChannel<ConsoleChannel>(channelName: "console")
    .AddAuditTrail(audit => audit.UseNDJson(options =>
    {
        options.DirectoryPath = auditDir;
        options.MaxFileSizeBytes = 1024 * 1024;        // 1 MB
        options.RollInterval = TimeSpan.FromMinutes(5); // time-based rolling
        options.MaxFileCount = 20;                      // keep at most 20 files
    }));
```

### 2. Registering a separate read-side

The reader is registered independently so a query API can live in a different process or
a different application from the publisher. Because the registration uses `TryAdd`, the
same `NdJsonAuditTrail` singleton is shared with the publisher when both run in the same
process.

```csharp
builder.Services.AddNDJsonAuditTrailQuerying(options =>
{
    options.DirectoryPath = auditDir;
});
```

### 3. Querying the audit trail

Reads are **asynchronous, non-blocking, and streamed**: each NDJSON file is opened with
`FileShare.ReadWrite` and read line-by-line via `StreamReader.ReadLineAsync`, so a
concurrent writer is never blocked and the full file content is never loaded into
memory. The `AuditTrailStreamQuery` filter is applied while streaming, before the
matched entries are sorted and yielded in chronological order.

```csharp
app.MapGet("/api/audit-trail", async (
    IAuditTrailReader<AuditTrailEntry> reader,
    string? eventType, string? source, string? subject,
    DateTimeOffset? from, DateTimeOffset? to, int? limit) =>
{
    var query = new AuditTrailStreamQuery
    {
        EventType = eventType, Source = source, Subject = subject,
        From = from, To = to
    };

    var entries = new List<AuditTrailEntry>();
    var max = limit ?? 100;
    await foreach (var entry in reader.ReadAsync(query))
    {
        if (entries.Count >= max) break;
        entries.Add(entry);
    }
    return Results.Ok(entries);
});
```

---

## File layout

Each audit file is named using a sortable, sequence-numbered pattern so multiple rolls
within the same second produce distinct files:

```
audit-trail/
  audit-trail-20260604-103012-000001.ndjson
  audit-trail-20260604-103017-000002.ndjson
  audit-trail-20260604-103024-000003.ndjson
```

Each line is a JSON-serialised `AuditTrailEntry`:

```json
{"id":"...","eventId":"...","eventType":"order.submitted","source":"...","subject":"ORD-...","timestamp":"2026-06-04T10:30:12.451+00:00","eventData":"{...}","storedAt":"2026-06-04T10:30:12.453+00:00"}
```

---

## Pluggable filesystem

All I/O goes through the `IFileSystem` abstraction from
[`System.IO.Abstractions`](https://www.nuget.org/packages/System.IO.Abstractions). To
back the audit trail with Azure Blob Storage, Amazon S3, or any other storage backend,
register a compatible `IFileSystem` implementation before `UseNDJson` is called:

```csharp
builder.Services.AddSingleton<IFileSystem>(sp => new MyAzureBlobFileSystem(connectionString));

builder.Services.AddEventPublisher(/* ... */)
    .AddAuditTrail(audit => audit.UseNDJson(o => o.DirectoryPath = "audit-trail"));
```

The `NdJsonAuditTrail` does not touch `System.IO` directly — every call goes through
`IFileSystem`, so any compatible adapter works without changes to the storage code.
