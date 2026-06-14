# Audit Trail

The **Audit Trail** feature creates an immutable, chronological record of all published events for compliance, auditing, and historical analysis.

## Why Use an Audit Trail?

Unlike the [Delivery Log](../delivery-log/README.md) (which tracks publish attempts) or [Dead-Letter](../dead-letter/README.md) (which captures failed deliveries), the Audit Trail serves a different purpose:

| Question | Audit Trail Answers |
|----------|---------------------|
| **Compliance** | "Prove that event X occurred at time T" |
| **History** | "Show me all events for order #123" |
| **Reconstruction** | "Rebuild the read model from scratch" |
| **Forensics** | "What happened in the system between 2pm and 3pm?" |
| **Regulatory** | "Retain all financial events for 7 years" |

## Architecture

```
IEventPublisher
    │
    ▼
[Middleware Pipeline]
    │
    ├── AuditTrailMiddleware
    │     captures event payload → writes to immutable store
    │
    ▼
Channel publish
```

The audit trail middleware sits in the publisher pipeline and records every event **before** it's sent to channels. This ensures:
- Events are recorded even if channel delivery fails
- The recorded event is exactly what was intended to be published
- Audit records are independent of delivery outcomes

## Core Concepts

### Immutable Store

Audit trail entries are **append-only**:
- Once written, entries cannot be modified
- Entries can be queried and filtered
- Old entries can be archived (but not altered)

### Event-Centric

Unlike delivery logs (which track attempts), the audit trail records:
- The event payload itself
- CloudEvents context attributes
- Timestamp when the event occurred (`CloudEvent.time`)
- Timestamp when recorded (`storedAt`)

### Pluggable Storage

The audit trail supports multiple backends:
- **NDJSON files** — Rolling files for file-based storage
- **Entity Framework Core** — Database-backed storage
- **Custom implementations** — Azure Blob, AWS S3, etc.

## Installation

### Core Package

```bash
dotnet add package Hermodr.AuditTrail
```

### Storage Backend Packages

#### NDJSON Backend (File-Based)

```bash
dotnet add package Hermodr.AuditTrail.NDJson
```

#### Entity Framework Core Backend

```bash
dotnet add package Hermodr.AuditTrail.EntityFramework
dotnet add package Microsoft.EntityFrameworkCore.Sqlite
```

## Quick Start

### Minimal Setup (NDJSON)

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
    options.RollInterval = TimeSpan.FromHours(6);
    options.MaxFileCount = 30;
}));
```

### With EF Core

```csharp
builder.Services.AddEventPublisher()
    .AddAuditTrail(audit => audit.UseEntityFramework(opts =>
        opts.UseSqlServer(connectionString)));
```

## Next Steps

| Page | Description |
|------|-------------|
| [Installation](installation.md) | Install packages and dependencies |
| [Concepts](concepts.md) | Understand audit trail vs delivery log vs event store |
| [NDJSON Backend](ndjson-backend.md) | File-based storage configuration |
| [Querying](querying.md) | Read and filter audit trail entries |
| [Filesystem Abstraction](filesystem-abstraction.md) | Pluggable storage (Azure Blob, S3, etc.) |
| [Examples](examples.md) | Usage patterns and scenarios |

## Related Pages

- [Reliability Patterns Overview](../README.md)
- [Delivery Log](../delivery-log/README.md)
- [Dead-Letter Handling](../dead-letter/README.md)
