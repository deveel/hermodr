# Dead-Letter Handling and Replay

Dead-letter handling lets an `EventPublisher` capture **failed channel deliveries** and route them to a fallback path instead of just logging the exception and moving on. In Deveel Events, the feature is implemented as an extension on top of the publisher's generic publish-error pipeline described in [Publish Error Handling](error-handling.md).

> **IMPORTANT**
> In normal conditions, prefer the native dead-letter capabilities provided by the underlying transport when they exist. Brokers and messaging platforms such as RabbitMQ, Azure Service Bus, and Amazon SQS already offer built-in dead-letter handling, and that native behavior is usually the recommended choice. This extension is mainly intended for channels that do not support dead-lettering natively, or for the uncommon case where you intentionally want to override the transport's built-in behavior.

Use it when you want to:

- inspect failed deliveries in-process
- persist failed `CloudEvent`s for later replay
- replay them manually on demand
- run a background worker that retries pending dead letters from durable storage

The feature is split into two packages:

| Package | Purpose |
|---------|---------|
| `Deveel.Events.Publisher.DeadLetter` | Dead-letter handlers, persistent replay abstractions, on-demand replay, background replay worker |
| `Deveel.Events.Publisher.DeadLetter.EntityFramework` | Entity Framework Core persistence for dead-letter messages |

## Why use dead-letter handling?

A transport failure is different from a modelling or serialization failure:

- the event was already created as a valid `CloudEvent`
- business code may need to know that delivery failed
- operators may want to inspect or replay the failed event later

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

## What failures are captured?

Dead-letter handlers run only for **channel publish failures** (`EventPublishStage.ChannelPublish`).

They do **not** run for:

- event creation failures
- `IEventConvertible` conversion failures

Those earlier failures go through the generic publisher error pipeline instead. If you need one hook for all publish stages, use `UseErrorHandler(...)` from `Deveel.Events.Publisher`; see [Publish Error Handling](error-handling.md).

## Installation

### Core dead-letter package

```bash
dotnet add package Deveel.Events.Publisher.DeadLetter
```

### Entity Framework Core integration

```bash
dotnet add package Deveel.Events.Publisher.DeadLetter.EntityFramework
dotnet add package Microsoft.EntityFrameworkCore.Sqlite
```

Replace `Sqlite` with the EF Core provider you actually use.

## In-process dead-letter handling

The lightest integration is `AddDeadLetter(...)`. It returns a `DeadLetterBuilder` that can register a callback or handler receiving `DeadLetterContext` whenever a channel throws while publishing. `UseHandler(...)` has overloads for synchronous callbacks, asynchronous callbacks, handler instances, and handler types.

```csharp
using Deveel;
using Deveel.Events;
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

### `DeadLetterContext`

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

### Behavior with `ThrowOnErrors`

Dead-letter handling does not hide the original publish outcome:

- when `ThrowOnErrors = false`, the handler still runs, but the original publish call does not throw
- when `ThrowOnErrors = true`, the handler runs **before** the publisher throws `EventPublishException`
- if the dead-letter handler itself throws, the publish call fails regardless of `ThrowOnErrors`

## Immediate replay through the pipeline

You can replay a dead-lettered event immediately by publishing the captured `CloudEvent` again from inside the handler.

`DeadLetterReplayPublishOptions` marks the second publish as a **replay attempt**, so a replay failure is not stored again as a brand-new dead letter.

```csharp
using Deveel;
using Deveel.Events;
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

- the fallback channel lives in the same process
- you want immediate rerouting instead of durable storage
- you want to keep business code unaware of the fallback logic

## Replay with the default in-memory store

If you only need replay inside a single host, `WithReplay()` registers replay services together with a default in-memory dead-letter store.

```csharp
services.AddEventPublisher(options =>
    {
        options.Source = new Uri("https://orders.example.com");
        options.ThrowOnErrors = false;
    })
    .AddRabbitMq(options => { /* transport config */ })
    .AddDeadLetter()
    .WithReplay();
```

This path uses:

- `DeadLetterMessage` as the default stored message type
- `InMemoryDeadLetterMessageStore` as the default backing store
- `DefaultDeadLetterMessageFactory` to capture failed deliveries

## Persisting dead letters

Storage is a dead-letter concern on its own: you can persist failed deliveries even if you do not enable replay yet.

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

### Storage contracts

| Contract | Purpose |
|----------|---------|
| `IDeadLetterMessage` | Persisted dead-letter payload plus replay metadata |
| `IDeadLetterMessageFactory<TMessage>` | Creates a stored message from `DeadLetterContext` |
| `IDeadLetterMessageStore` | Persists messages and manages replay state transitions |
| `IDeadLetterMessageReplayer` | Replays a stored message through a publisher pipeline |
| `IDeadLetterReplayProcessor` | Processes pending messages in batches |

### `IDeadLetterMessage`

Every persisted message exposes:

- the original `CloudEvent`
- the originating publisher name
- channel metadata (`ChannelName`, `ChannelType`)
- the last error message
- replay state (`Status`, `ReplayCount`, `NextReplayAt`)

## Replay from persistent storage

Call `WithReplay(...)` to register an on-demand replayer:

```csharp
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

services.AddEventPublisher(options =>
    {
        options.Source = new Uri("https://orders.example.com/publisher");
        options.ThrowOnErrors = false;
    })
    .AddChannel<FailingChannel>()
    .AddDeadLetter()
    .UseRepository<MyDeadLetterMessage, MyDeadLetterStore>()
    .WithFactory<MyDeadLetterMessage, MyDeadLetterMessageFactory>()
    .WithReplay(options => options.TransportPublisherName = "transport")
    ;
```

Then replay a message manually:

```csharp
var replayer = provider.GetRequiredService<IDeadLetterMessageReplayer>();
await replayer.ReplayAsync(messageId, cancellationToken);
```

### Which publisher is used for replay?

Replay resolution follows this order:

1. `DeadLetterReplayOptions.TransportPublisherName`
2. `IDeadLetterMessage.PublisherName`
3. the default `IEventPublisher`

This makes it possible to:

- replay through the same pipeline that originally failed
- replay through a dedicated transport-only pipeline
- split persistence and transport responsibilities across hosts

### Important replay behavior

`WithReplay(...)` forces `EventPublisherOptions.ThrowOnErrors = true` for the replay path. This is intentional: replay failures must surface so the store can update retry state correctly.

## Background replay worker

Call `WithReplayWorker(...)` to add a hosted service that polls the store and replays pending dead letters in the background.

```csharp
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
    })
    ;
```

The worker resolves `IDeadLetterReplayProcessor`, reads eligible pending messages, and replays them one by one.

## Replay lifecycle

Stored dead letters move through a small, explicit state machine:

| Status | Meaning |
|--------|---------|
| `Pending` | Waiting to be replayed |
| `Replaying` | Currently being replayed |
| `Replayed` | Replay succeeded |
| `Failed` | Retry limit exceeded; no longer scheduled automatically |

`IDeadLetterMessageStore` is responsible for the corresponding transitions:

| Method | Typical effect |
|--------|----------------|
| `AddAsync(...)` | Save new message as `Pending` |
| `SetReplayingAsync(...)` | Mark message as `Replaying` |
| `SetReplayedAsync(...)` | Mark message as `Replayed` and clear `NextReplayAt` |
| `SetRetryAsync(...)` | Increment replay count, store error, schedule next attempt |
| `SetFailedAsync(...)` | Mark message as permanently failed |
| `GetPendingMessagesAsync(...)` | Return pending messages whose `NextReplayAt` is due |

## Entity Framework Core integration

`Deveel.Events.Publisher.DeadLetter.EntityFramework` provides a ready-made EF implementation:

| Type | Purpose |
|------|---------|
| `DbDeadLetterMessage` | Concrete dead-letter entity implementing `IDeadLetterMessage` |
| `DbDeadLetterAttribute` | Stores CloudEvents extension attributes |
| `DeadLetterDbContext` | Minimal EF `DbContext` for dead-letter storage |
| `EntityDeadLetterMessageStore<TMessage>` | EF-backed `IDeadLetterMessageStore` implementation |
| `DefaultDeadLetterMessageFactory<TMessage>` | Converts `DeadLetterContext` into `DbDeadLetterMessage` |

### Recommended setup

```csharp
using Deveel;
using Deveel.Events;
using Microsoft.EntityFrameworkCore;

services.AddEventPublisher(options =>
    {
        options.Source = new Uri("https://orders.example.com");
        options.ThrowOnErrors = false;
    })
    .AddRabbitMq(options =>
    {
        options.ConnectionString = "amqp://guest:guest@localhost:5672";
        options.ExchangeName = "orders";
    })
    .AddDeadLetter()
    .WithEntityFramework(options =>
        options.UseSqlite("Data Source=deadletters.db"))
    .WithReplayWorker(options =>
    {
        options.Interval = TimeSpan.FromSeconds(30);
        options.MaxRetryCount = 3;
        options.RetryInterval = TimeSpan.FromMinutes(1);
    })
    ;
```

### What `DbDeadLetterMessage` stores

`DbDeadLetterMessage` maps the most important CloudEvents data as columns:

- `Id`, `EventType`, `Source`, `Subject`, `EventTime`
- `DataContentType`, `DataSchema`
- event payload in `DataText` or `DataBytes`
- replay metadata such as `Status`, `ReplayCount`, `NextReplayAt`, `CreatedAt`, `LastStatusAt`
- channel and publisher metadata (`PublisherName`, `ChannelName`, `ChannelType`)

CloudEvents extension attributes are stored in child rows through `DbDeadLetterAttribute`.

### Creating the database

The package provides model configuration through `DeadLetterDbContext`. Create the database the same way you would for any other EF Core context:

```csharp
await using var scope = host.Services.CreateAsyncScope();
var db = scope.ServiceProvider.GetRequiredService<DeadLetterDbContext>();
await db.Database.EnsureCreatedAsync();
```

Or use migrations if you want the dead-letter tables to be managed as part of your normal schema lifecycle.

## Same-process vs cross-process deployment

### Same-process

Register:

- the failing publisher pipeline
- the dead-letter store
- `WithReplayWorker(...)` in the same host

This works well when:

- the application itself owns recovery
- polling load is small
- operational simplicity matters more than independent scaling

### Cross-process

Split the feature across two applications:

1. **publisher app** — publishes to the real transport and persists failures into the dead-letter store
2. **worker app** — connects to the same store and runs `WithReplayWorker(...)`

This is the recommended shape when:

- replay load must scale independently
- the replay worker uses a different transport pipeline
- you want to keep HTTP/API processes free from polling work

## Options reference

### `DeadLetterReplayOptions`

| Property | Default | Meaning |
|----------|---------|---------|
| `Interval` | `00:00:30` | Polling interval for the background worker |
| `MaxBatchSize` | `0` | Maximum number of pending messages processed per cycle (`0` means no explicit limit) |
| `TransportPublisherName` | empty | Named publisher pipeline used for replay; when empty, the stored publisher name is used |
| `MaxRetryCount` | `3` | Maximum automatic replay attempts before marking the message as `Failed` |
| `RetryInterval` | `00:01:00` | Delay before the next replay attempt after a failure |

### `DeadLetterReplayPublishOptions`

This is a specialized `EventPublishOptions` wrapper used internally by replay logic and by advanced custom replay flows. It marks a publish call as a replay attempt while forwarding any inner channel options.

In most applications you do not need to create it yourself unless you are implementing your own immediate replay callback.

## Related pages

- [Publisher Channels Overview](README.md)
- [Transactional Outbox](outbox.md)
- [OrderService — In-Process Dead-Letter Replay](../samples/deadletter-inproc.md)
- [OrderService — Split Dead-Letter Replay with Entity Framework](../samples/deadletter-relay-entityframework.md)
