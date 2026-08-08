# Dead-Letter Persistence

Storage is a dead-letter concern on its own: you can persist failed deliveries even if you don't enable replay yet.

## Storage Contracts

| Contract | Purpose |
|----------|---------|
| `IDeadLetterMessage` | Persisted dead-letter payload plus replay metadata |
| `IDeadLetterMessageFactory<TMessage>` | Creates a stored message from `DeadLetterContext` |
| `IDeadLetterMessageStore` | Persists messages and manages replay state transitions |
| `IDeadLetterMessageReplayer` | Replays a stored message through a publisher pipeline |
| `IDeadLetterReplayProcessor` | Processes pending messages in batches |

## Registration with Custom Store

```csharp
services.AddEventPublisher(options =>
    {
        options.Source = new Uri("https://orders.example.com");
        options.ThrowOnErrors = false;
    })
    .AddRabbitMq(options => { /* transport config */ })
    .AddDeadLetter()
    .UseRepository<MyDeadLetterMessage, MyDeadLetterStore>()
    .WithFactory<MyDeadLetterMessage, MyDeadLetterMessageFactory>();
```

`AddDeadLetter()` exposes storage as a dead-letter sub-feature. Use `UseRepository<TMessage, TStore>()` plus `WithFactory<TMessage, TFactory>()` when you want to persist failed deliveries with a custom storage model, even if replay is not enabled.

The dead-letter handler converts `DeadLetterContext` into `TMessage` and saves it through the configured `IDeadLetterMessageStore`.

## IDeadLetterMessage

Every persisted message exposes:

- The original `CloudEvent`
- The originating publisher name
- Channel metadata (`ChannelName`, `ChannelType`)
- The last error message
- Replay state (`Status`, `ReplayCount`, `NextReplayAt`)

### Example Custom Message

```csharp
using CloudNative.CloudEvents;
using Hermodr.Publisher.DeadLetter;

public class MyDeadLetterMessage : IDeadLetterMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    
    public CloudEvent Event { get; set; } = default!;
    public string PublisherName { get; set; } = default!;
    public string? ChannelName { get; set; }
    public Type? ChannelType { get; set; }
    public string ErrorMessage { get; set; } = default!;
    
    public DeadLetterMessageStatus Status { get; set; } = DeadLetterMessageStatus.Pending;
    public int ReplayCount { get; set; }
    public DateTimeOffset? NextReplayAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastStatusAt { get; set; } = DateTimeOffset.UtcNow;
}
```

## Timing Metadata in Dead-Letter Records

Dead-letter records intentionally separate event history from replay workflow timing:

| Field | Meaning |
|-------|---------|
| `EventTime` / `CloudEvent.time` | When the original event occurred |
| `CreatedAt` | When the dead-letter record was created |
| `LastStatusAt` | When replay status last changed (`Replaying`, `Replayed`, `Failed`) |
| `NextReplayAt` | Earliest time the next replay attempt is allowed |

`NextReplayAt` and `LastStatusAt` are operational scheduling markers, not business timestamps. Replay components compute them through the configured system-time abstraction (`IEventSystemTime`) so tests can freeze time and assert exact values.

## Related Pages

- [Dead-Letter Overview](README.md)
- [In-Process Handling](in-process-handling.md)
- [Replay](replay.md)
