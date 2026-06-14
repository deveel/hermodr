# Dead-Letter Replay

Call `WithReplay()` to register an on-demand replayer that can replay stored dead-letter messages through a publisher pipeline.

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

// Register dead-letter with replay
services.AddEventPublisher(options =>
    {
        options.Source = new Uri("https://orders.example.com/publisher");
        options.ThrowOnErrors = false;
    })
    .AddChannel<FailingChannel>()
    .AddDeadLetter()
    .UseRepository<MyDeadLetterMessage, MyDeadLetterStore>()
    .WithFactory<MyDeadLetterMessage, MyDeadLetterMessageFactory>()
    .WithReplay(options => options.TransportPublisherName = "transport");
```

## Manual Replay

Once configured, replay a message manually:

```csharp
var replayer = provider.GetRequiredService<IDeadLetterMessageReplayer>();
await replayer.ReplayAsync(messageId, cancellationToken);
```

## Which Publisher Is Used for Replay?

Replay resolution follows this order:

1. `DeadLetterReplayOptions.TransportPublisherName`
2. `IDeadLetterMessage.PublisherName`
3. The default `IEventPublisher`

This makes it possible to:
- Replay through the same pipeline that originally failed
- Replay through a dedicated transport-only pipeline
- Split persistence and transport responsibilities across hosts

## Important Replay Behavior

`WithReplay(...)` forces `EventPublisherOptions.ThrowOnErrors = true` for the replay path. This is intentional: replay failures must surface so the store can update retry state correctly.

## Related Pages

- [Dead-Letter Overview](README.md)
- [Persistence](persistence.md)
- [Background Worker](background-worker.md)
