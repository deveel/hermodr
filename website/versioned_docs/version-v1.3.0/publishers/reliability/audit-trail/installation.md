# Audit Trail Installation

## Prerequisites

- .NET 8 or later
- Entity Framework Core 8+ (for EF Core backend)
- Sufficient storage space for audit records (plan for retention requirements)

## Package Installation

### Core Audit Trail Package

The core package provides the middleware and base abstractions:

```bash
dotnet add package Hermodr.AuditTrail
```

### Storage Backend Packages

Choose one or more storage backends based on your needs:

#### NDJSON Backend (File-Based, Recommended for Most Scenarios)

```bash
dotnet add package Hermodr.AuditTrail.NDJson
```

#### Entity Framework Core Backend (Database-Backed)

```bash
dotnet add package Hermodr.AuditTrail.EntityFramework
dotnet add package Microsoft.EntityFrameworkCore.Sqlite
```

Replace `Sqlite` with your actual provider (`SqlServer`, `Npgsql`, `PostgreSQL`, etc.).

## Package Overview

| Package | Purpose | Required |
|---------|---------|----------|
| `Hermodr.AuditTrail` | Core middleware and abstractions | ✅ Yes |
| `Hermodr.AuditTrail.NDJson` | NDJSON file-based backend | For file storage |
| `Hermodr.AuditTrail.EntityFramework` | EF Core database backend | For database storage |
| `Microsoft.EntityFrameworkCore.*` | Database provider | For EF Core backend |

## What's Included

### Core Package

- `AuditTrailMiddleware` — Middleware that records every published event
- `IAuditTrailWriter` — Write interface for audit entries
- `IAuditTrailReader` — Read interface for querying audit entries
- `AuditTrailEntry` — Data contract for stored audit records

### NDJSON Package

- `NdJsonAuditTrail` — NDJSON file-based implementation
- `AuditTrailStreamQuery` — Stream-based query API
- File rolling and retention management
- `IFileSystem` abstraction for pluggable storage

### EF Core Package

- `AuditTrailDbContext` — EF Core context with audit entities
- `DbAuditTrailEntry` — Entity mapping
- `EntityAuditTrailRepository` — Repository implementation
- LINQ query support

## Storage Requirements

### NDJSON Backend

- **Disk space:** Plan for ~1-10 MB per 10,000 events (depends on event size)
- **File retention:** Default keeps 30 files, configurable
- **I/O:** Sequential writes, concurrent reads supported

### EF Core Backend

- **Database:** Requires relational database (SQL Server, PostgreSQL, SQLite, etc.)
- **Table size:** Grows linearly with event count
- **Indexing:** Recommended on `EventType`, `Timestamp`, `Subject` columns
- **Retention:** Implement archival/deletion policy for compliance

## Next Steps

After installation:

1. **[Concepts](concepts.md)** — Understand audit trail scope and use cases
2. **[NDJSON Backend](ndjson-backend.md)** — Configure file-based storage
3. **[Querying](querying.md)** — Learn to read and filter audit entries
4. **[Examples](examples.md)** — See real-world usage patterns

## Related Pages

- [Audit Trail Overview](README.md)
- [Reliability Patterns](../README.md)
