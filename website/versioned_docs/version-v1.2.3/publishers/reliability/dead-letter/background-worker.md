# Dead-Letter Background Worker

Call `WithReplayWorker()` to add a hosted service that polls the store and replays pending dead letters in the background.

## Registration

```csharp
// Register a named publisher pipeline for replay
services.AddEventPublisher("transport", builder => builder
    .Configure(options =>
    {
        options.Source = new Uri("https://orders.example.com/transport");
        options.ThrowOnErrors = true;
    })
    .AddRabbitMq(options =>
    {
        options.ConnectionString = "amqp://guest:guest@localhost:5672";
        options.ExchangeName = "orders";
    }));

// Register dead-letter with background worker
services.AddEventPublisher(options => options.Source = new Uri("https://orders.example.com/replay"))
    .AddDeadLetter()
    .UseRepository<MyDeadLetterMessage, MyDeadLetterStore>()
    .WithFactory<MyDeadLetterMessage, MyDeadLetterMessageFactory>()
    .WithReplayWorker(options =>
    {
        options.TransportPublisherName = "transport";
        options.Interval = TimeSpan.FromSeconds(15);
        options.MaxBatchSize = 50;
        options.MaxRetryCount = 5;
        options.RetryInterval = TimeSpan.FromMinutes(2);
    });
```

## DeadLetterReplayOptions

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Interval` | `TimeSpan` | `00:00:30` | Polling interval for the background worker |
| `MaxBatchSize` | `int` | `0` | Maximum pending messages processed per cycle (`0` = no limit) |
| `TransportPublisherName` | `string` | empty | Named publisher pipeline used for replay |
| `MaxRetryCount` | `int` | `3` | Maximum automatic replay attempts before marking as `Failed` |
| `RetryInterval` | `TimeSpan` | `00:01:00` | Delay before the next replay attempt after a failure |

## How the Worker Works

The worker resolves `IDeadLetterReplayProcessor`, reads eligible pending messages, and replays them one by one:

1. **Query** — Get pending messages where `NextReplayAt <= UtcNow`
2. **Claim** — Mark each message as `Replaying` to prevent duplicate processing
3. **Replay** — Publish the message through the configured publisher
4. **Update** — On success: mark as `Replayed`; on failure: schedule retry or mark as `Failed`
5. **Repeat** — Continue until `MaxBatchSize` is reached or no more pending messages

## Related Pages

- [Dead-Letter Overview](README.md)
- [Replay](replay.md)
- [Deployment](deployment.md)
