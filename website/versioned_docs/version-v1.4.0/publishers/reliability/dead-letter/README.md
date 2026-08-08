# Dead-Letter Handling and Replay

Dead-letter handling captures **failed channel deliveries** and routes them to a fallback path instead of just logging the exception and moving on.

## Why Use Dead-Letter Handling?

A transport failure is different from a modelling or serialization failure:

- The event was already created as a valid `CloudEvent`
- Business code may need to know that delivery failed
- Operators may want to inspect or replay the failed event later

Dead-letter handling addresses that specific case.

```
EventPublisher
    │
    ▼
channel publish
    │
    ├── success ──► done
    │
    └── failure ──► dead-letter handler
                         │
                         ├── inspect / log
                         ├── persist
                         └── replay now or later
```

> **IMPORTANT:** In normal conditions, prefer the native dead-letter capabilities provided by the underlying transport when they exist. Brokers such as RabbitMQ, Azure Service Bus, and Amazon SQS already offer built-in dead-letter handling. This extension is mainly intended for channels that don't support dead-lettering natively, or for the uncommon case where you intentionally want to override the transport's built-in behavior.

## What Failures Are Captured?

Dead-letter handlers run **only** for **channel publish failures** (`EventPublishStage.ChannelPublish`).

They do **not** run for:
- Event creation failures
- `IEventConvertible` conversion failures

Those earlier failures go through the generic publisher error pipeline instead. If you need one hook for all publish stages, use `UseErrorHandler(...)` from `Hermodr.Publisher`; see [Publish Error Handling](../../error-handling.md).

## Installation

### Core Dead-Letter Package

```bash
dotnet add package Hermodr.Publisher.DeadLetter
```

### Entity Framework Core Integration

```bash
dotnet add package Hermodr.Publisher.DeadLetter.EntityFramework
dotnet add package Microsoft.EntityFrameworkCore.Sqlite
```

Replace `Sqlite` with the EF Core provider you actually use.

## Next Steps

| Page | Description |
|------|-------------|
| [In-Process Handling](in-process-handling.md) | Register handlers, use DeadLetterContext, immediate replay |
| [Persistence](persistence.md) | Store dead letters in custom or EF Core backends |
| [Replay](replay.md) | Replay stored messages on-demand |
| [Background Worker](background-worker.md) | Automatic retry with hosted service |
| [EF Core Integration](ef-core-integration.md) | Ready-made EF implementation |
| [Deployment](deployment.md) | Same-process vs cross-process topologies |

## Related Pages

- [Reliability Patterns Overview](../README.md)
- [Transactional Outbox](../outbox/README.md)
- [Delivery Log](../delivery-log/README.md)
- [Publish Error Handling](../../error-handling.md)
