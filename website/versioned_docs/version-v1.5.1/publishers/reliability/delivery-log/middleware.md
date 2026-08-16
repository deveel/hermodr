# Delivery Log Middleware

The middleware is an `IEventMiddleware` registered via `EventPublisherBuilder.Use<T>()`. It sits in the publisher pipeline and records every publish attempt.

## How It Works

On every publish call, the middleware:

1. **Captures start time** — Uses `IEventSystemTime.UtcNow` as the start time
2. **Reads attempt number** — From `EventContext` (initializes to 2 on first call, returns 1)
3. **Executes pipeline** — Calls `next(context)`
4. **Records outcome**:
   - On success: `Outcome = Succeeded`
   - On exception: `Outcome = Failed`, captures error details, re-throws exception
5. **Writes record** — Constructs `EventDeliveryRecord` and calls `IEventPublishDeliveryLog.RecordAsync()`

## Key Characteristics

| Characteristic | Description |
|----------------|-------------|
| **Never swallows exceptions** — The middleware always re-throws publish failures |
| **Storage failures are logged** — If the log write itself fails, the exception is logged and swallowed |
| **Uses time abstraction** — `IEventSystemTime` instead of `DateTimeOffset.UtcNow` for testability |
| **Tracks attempt number** — Via `EventContext` for retry scenarios |

## Time Abstraction

Using `IEventSystemTime` instead of direct `DateTimeOffset.UtcNow` calls makes timestamps deterministic in tests:

```csharp
public class FrozenSystemTime : IEventSystemTime
{
    public DateTimeOffset UtcNow { get; } = new DateTimeOffset(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);
}

// In tests:
services.AddSingleton<IEventSystemTime, FrozenSystemTime>();
```

## Registration

```csharp
builder.Services
    .AddEventPublisher()
    .AddDeliveryLog(log => log.UseInMemory());
```

The middleware is automatically added to the publisher pipeline when you call `AddDeliveryLog()`.

## Related Pages

- [Delivery Log Overview](README.md)
- [Error Handler](error-handler.md)
- [Registration](registration.md)
