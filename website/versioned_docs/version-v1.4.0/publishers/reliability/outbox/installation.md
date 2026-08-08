# Outbox Installation

## Prerequisites

- .NET 8 or later
- Entity Framework Core 8+ (recommended for the EF Core integration)
- A message broker (RabbitMQ, Azure Service Bus, etc.) for the relay to publish to

## Package Installation

### Step 1: Install the Core Outbox Package

The core package provides the outbox channel, relay service, and base contracts:

```bash
dotnet add package Hermodr.Publisher.Outbox
```

### Step 2: Install Entity Framework Core Integration (Recommended)

The EF Core package provides ready-made implementations that eliminate most boilerplate:

```bash
dotnet add package Hermodr.Publisher.Outbox.EntityFramework
```

### Step 3: Install Your Database Provider

Choose the EF Core provider for your database:

```bash
# SQL Server
dotnet add package Microsoft.EntityFrameworkCore.SqlServer

# PostgreSQL
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL

# SQLite (for development/testing)
dotnet add package Microsoft.EntityFrameworkCore.Sqlite

# MySQL
dotnet add package Pomelo.EntityFrameworkCore.MySql
```

## Package Overview

| Package | Purpose | Required |
|---------|---------|----------|
| `Hermodr.Publisher.Outbox` | Core outbox channel and relay service | ✅ Yes |
| `Hermodr.Publisher.Outbox.EntityFramework` | EF Core entity, repository, and extensions | ✅ Recommended |
| `Microsoft.EntityFrameworkCore.*` | Database provider | ✅ For EF Core integration |

## What's Included

### Core Package (`Hermodr.Publisher.Outbox`)

- `OutboxPublishChannel<TMessage>` — Channel that persists events instead of sending directly
- `IOutboxMessage` — Contract for outbox message entities
- `IOutboxMessageFactory<TMessage>` — Creates outbox records from `CloudEvent` instances
- `IOutboxMessageStore<TMessage>` — Repository for persisting and querying outbox records
- `OutboxRelayService` — Background service that polls and forwards pending messages
- `OutboxMessageStatus` — Enum tracking delivery lifecycle (`Pending`, `Sending`, `Delivered`, `Failed`)

### EF Core Package (`Hermodr.Publisher.Outbox.EntityFramework`)

- `DbOutboxMessage` — Ready-to-use `IOutboxMessage` implementation with EF mappings
- `EntityOutboxMessageRepository<TMessage>` — EF Core repository implementation
- `OutboxDbContext` — Minimal `DbContext` with outbox entity configured
- `WithEntityFramework()` — Fluent extension that wires all EF components in one call
- `AddEntityFrameworkOutbox()` — Convenience method combining outbox + EF registration

## Next Steps

After installation, proceed to:

1. **[Components](components.md)** — Learn about the outbox contracts (entity, factory, store)
2. **[EF Core Integration](ef-core-integration.md)** — Set up the ready-made EF implementation
3. **[Registration](registration.md)** — Wire everything up in your application

## Related Pages

- [Reliability Patterns Overview](../README.md)
- [Transactional Outbox Overview](README.md)
