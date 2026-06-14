# In-Process Dead-Letter Handling

The lightest integration is `AddDeadLetter(...)`. It returns a `DeadLetterBuilder` that can register a callback or handler receiving `DeadLetterContext` whenever a channel throws while publishing.

## Registration

### With Inline Handler

```csharp
using Hermodr;
using Microsoft.Extensions.DependencyInjection;

builder.Services
    .AddEventPublisher(options =>
    {
        options.Source = new Uri("https://orders.example.com");
        options.ThrowOnErrors = false;
    })
    .AddRabbitMq(options =>
    {
        options.ConnectionString = "amqp://guest:guest@localhost:5672";
        options.ExchangeName = "orders";
    })
    .AddDeadLetter(deadLetter => deadLetter.UseHandler(context =>
    {
        Console.WriteLine(
            $"Dead-lettered {context.Event.Type} " +
            $"from channel {context.ChannelName ?? context.ChannelType?.Name}: " +
            $"{context.Exception.Message}");
    }));
```

### With Handler Class

```csharp
public class LoggingDeadLetterHandler : IDeadLetterHandler
{
    private readonly ILogger<LoggingDeadLetterHandler> _logger;

    public LoggingDeadLetterHandler(ILogger<LoggingDeadLetterHandler> logger)
        => _logger = logger;

    public Task HandleAsync(DeadLetterContext context, CancellationToken ct)
    {
        _logger.LogError(
            context.Exception,
            "Dead-lettered event {EventType} from channel {Channel}: {Error}",
            context.Event.Type,
            context.ChannelName ?? context.ChannelType?.Name,
            context.Exception.Message);

        return Task.CompletedTask;
    }
}

// Registration
builder.Services
    .AddEventPublisher()
    .AddRabbitMq(/* config */)
    .AddDeadLetter()
    .UseHandler<LoggingDeadLetterHandler>();
```

## DeadLetterContext

`DeadLetterContext` gives your handler the information you usually need for logging, fallback delivery, or persistence:

| Property | Meaning |
|----------|---------|
| `PublisherName` | Name of the publisher pipeline that failed |
| `Event` | The `CloudEvent` whose delivery failed |
| `Exception` | The thrown exception |
| `Services` | Scoped service provider for the current publish attempt |
| `CancellationToken` | Publish cancellation token |
| `Options` | Effective per-call publish options |
| `ChannelType` | Concrete channel type involved in the failure |
| `ChannelName` | Logical channel name, when available |

## Behavior with ThrowOnErrors

Dead-letter handling does not hide the original publish outcome:

| Scenario | Behavior |
|----------|----------|
| `ThrowOnErrors = false` | Handler runs, but publish call does not throw |
| `ThrowOnErrors = true` | Handler runs **before** the publisher throws `EventPublishException` |
| Handler itself throws | Publish call fails regardless of `ThrowOnErrors` |

## Immediate Replay Through the Pipeline

You can replay a dead-lettered event immediately by publishing the captured `CloudEvent` again from inside the handler.

`DeadLetterReplayPublishOptions` marks the second publish as a **replay attempt**, so a replay failure is not stored again as a brand-new dead letter.

```csharp
using Hermodr;
using Microsoft.Extensions.DependencyInjection;

builder.Services
    .AddEventPublisher(options =>
    {
        options.Source = new Uri("https://orders.example.com");
        options.ThrowOnErrors = true;
    })
    .AddChannel<PrimaryChannel>(channelName: "primary")
    .AddChannel<RecoveryChannel>(channelName: "recovery")
    .AddDeadLetter(deadLetter => deadLetter.UseHandler(async context =>
    {
        var publisher = context.Services.GetRequiredService<IEventPublisher>();

        await publisher.PublishEventAsync(
            context.Event,
            new DeadLetterReplayPublishOptions(
                new NamedChannelPublishOptions("recovery")),
            context.CancellationToken);
    }));
```

This pattern is useful when:
- The fallback channel lives in the same process
- You want immediate rerouting instead of durable storage
- You want to keep business code unaware of the fallback logic

## Related Pages

- [Dead-Letter Overview](README.md)
- [Persistence](persistence.md)
- [Replay](replay.md)
