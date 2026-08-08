# Delivery Log Installation

## Prerequisites

- .NET 8 or later
- Microsoft.Extensions.Logging.Abstractions (for logging integration)

## Package Installation

### Core Package

The core package provides the middleware and error handler:

```bash
dotnet add package Hermodr.Publisher.DeliveryLog
```

### Storage Backend Packages

Choose one or more storage backends based on your needs:

#### In-Memory (Development/Testing)

```bash
dotnet add package Hermodr.Publisher.DeliveryLog.InMemory
```

#### NDJSON Rolling Files (Production, File-Based)

```bash
dotnet add package Hermodr.Publisher.DeliveryLog.NDJson
```

#### Entity Framework Core (Production, Database)

```bash
dotnet add package Hermodr.Publisher.DeliveryLog.EntityFramework
dotnet add package Microsoft.EntityFrameworkCore.Sqlite
```

Replace `Sqlite` with your actual provider (`SqlServer`, `Npgsql`, etc.).

## Package Overview

| Package | Purpose | Required |
|---------|---------|----------|
| `Hermodr.Publisher.DeliveryLog` | Core middleware and error handler | ✅ Yes |
| `Hermodr.Publisher.DeliveryLog.InMemory` | In-memory storage backend | For testing |
| `Hermodr.Publisher.DeliveryLog.NDJson` | NDJSON file-based backend | For file storage |
| `Hermodr.Publisher.DeliveryLog.EntityFramework` | EF Core backend | For database storage |
| `Microsoft.EntityFrameworkCore.*` | Database provider | For EF Core backend |

## What's Included

### Core Package

- `DeliveryLogMiddleware` — Middleware that records every publish attempt
- `DeliveryLogPublishErrorHandler` — Error handler for the failure path
- `EventDeliveryRecord` — Data contract for delivery telemetry
- `EventDeliveryOutcome` — Three-state outcome enum
- `IEventPublishDeliveryLog` — Storage abstraction

### Storage Packages

Each storage package provides:
- Implementation of `IEventPublishDeliveryLog`
- Storage-specific configuration options
- Query APIs for reading delivery records (where applicable)

## Next Steps

After installation, proceed to:

1. **[Middleware](middleware.md)** — Understand how the middleware works
2. **[Error Handler](error-handler.md)** — Learn about the error path capture
3. **[Registration](registration.md)** — Wire everything up
4. **[Storage Backends](storage-backends.md)** — Choose and configure your backend

## Related Pages

- [Delivery Log Overview](README.md)
- [Reliability Patterns](../README.md)
