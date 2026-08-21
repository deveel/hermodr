# Delivery Log

The `Hermodr.Publisher.DeliveryLog` package records operational telemetry for every event publish attempt: which channel was used, when the attempt happened, how many times it was retried, how long it took, whether it succeeded or failed, and what error occurred.

## Why Use the Delivery Log?

The existing [Dead-Letter Channel](../dead-letter/README.md) captures only failed events and preserves them for replay. The Audit Trail (planned) records domain facts — the event payloads themselves — for auditing and read-model rebuilding. Neither answers operational questions about the *publishing infrastructure*:

- How many times did we attempt to send event X before it succeeded?
- Which channel is producing the most failures?
- What was the average delivery latency last week?
- Did a specific subscriber receive all events during an outage window?

The Delivery Log fills this gap. It records structured, queryable telemetry about every delivery attempt — successes, failures, retries — so you can monitor publish health, compute SLAs, and debug delivery issues without relying on broker-specific dashboards or generic application logs.

## Architecture

```
IEventPublisher
    │
    ▼
[Middleware Pipeline]
    │
    ├── DeliveryLogMiddleware
    │     captures start time → calls next → writes outcome + elapsed
    │
    ▼
Channel publish
    │
    ├── success ──► record Succeeded
    │
    └── failure ──► middleware records Failed (re-throws exception)
                     │
                     └── DeliveryLogPublishErrorHandler (optional)
                          records failure for ThrowOnErrors=false path
```

## Core Types

The feature is built on three types, each with a focused responsibility:

### IEventPublishDeliveryLog

The write-only surface. It exposes a single method:

```csharp
public interface IEventPublishDeliveryLog
{
    Task RecordAsync(EventDeliveryRecord record, CancellationToken cancellationToken = default);
}
```

The middleware depends on this interface, which keeps it decoupled from query capabilities.

### EventDeliveryRecord

The concrete data contract for one delivery attempt. It carries:

- The event itself
- Publisher metadata
- Attempt number
- Timestamp
- Outcome (Succeeded, Failed, Retried)
- Error details (type, message)
- Elapsed time

### EventDeliveryOutcome

Three-state enum:

| Value | Meaning |
|-------|---------|
| `Succeeded` | Delivered without exception |
| `Failed` | Terminal failure |
| `Retried` | Failure with retry scheduled (reserved for future retry infrastructure) |

## Next Steps

| Page | Description |
|------|-------------|
| [Installation](installation.md) | Install packages |
| [Middleware](middleware.md) | How DeliveryLogMiddleware works |
| [Error Handler](error-handler.md) | Capture failures on error path |
| [Registration](registration.md) | Wire up in DI |
| [Storage Backends](storage-backends.md) | In-Memory, NDJSON, EF Core comparison |
| [NDJSON Backend](ndjson-backend.md) | Detailed NDJSON configuration |
| [EF Core Backend](ef-core-backend.md) | Detailed EF Core configuration |
| [Comparison](comparison.md) | vs dead-letter, vs audit trail |

## Related Pages

- [Reliability Patterns Overview](../README.md)
- [Dead-Letter Handling](../dead-letter/README.md)
- [Audit Trail](../audit-trail/README.md)
- [Publish Error Handling](../../error-handling.md)
