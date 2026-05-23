# Publish Error Handling

The publisher exposes a general-purpose error handling capability through `IEventPublishErrorHandler` and `UseErrorHandler(...)`.

This capability is broader than dead-letter handling. Dead-letter handling is one implementation built on top of it, but you can also use the same hook for logging, telemetry, alerting, custom persistence, compensating actions, or selective policy enforcement around publish failures.

## Why this exists

Publishing can fail at more than one stage:

| Stage | Meaning |
|-------|---------|
| `EventPublishStage.EventCreation` | Building a `CloudEvent` from event data failed |
| `EventPublishStage.EventConversion` | Converting an `IEventConvertible` to `CloudEvent` failed |
| `EventPublishStage.ChannelPublish` | Dispatching a `CloudEvent` to a channel failed |

`IEventPublishErrorHandler` gives you one extension point for all of those stages.

## Pipeline middleware vs. publish error handlers

The regular publish pipeline and publish error handlers solve different problems.

| Aspect | Publish pipeline / middleware | Publish error handler |
|--------|-------------------------------|-----------------------|
| Primary purpose | Shape, enrich, route, validate, or short-circuit the normal publish flow | Observe and react after a publish stage has failed |
| Registration API | `Use<TMiddleware>()`, `UseWhen<TMiddleware>(...)` | `UseErrorHandler(...)` |
| When it runs | During the normal publish path, before and around the terminal dispatch step | Only after a publish stage throws |
| Main input | `EventContext` | `EventPublishErrorContext` |
| Can change the outgoing event? | Yes | No, it reacts to a failure that already happened |
| Can short-circuit publishing? | Yes, by not calling `next(context)` | No, it is not part of the main pipeline chain |
| Typical use cases | correlation, enrichment, validation, routing, metrics around successful flow | logging failures, auditing, alerts, dead-letter capture, failure-specific policies |

In short:

- middleware is part of the **main publish execution**
- error handlers are part of the **failure reaction path**

### When to choose middleware

Use middleware when you need to influence how publishing happens:

- add or mutate CloudEvent attributes
- inspect or rewrite publish options
- share data through `EventContext.Items`
- enforce pre-dispatch policies
- short-circuit or wrap the normal publish flow

See [Publish Pipeline & Middleware](../concepts/publish-pipeline.md) for the full middleware model.

### When to choose an error handler

Use an error handler when you need to react to a failure after it occurs:

- record diagnostics about a failed publish attempt
- emit telemetry or alerts
- persist failed deliveries for later processing
- apply stage-specific recovery logic

### Important boundary

Middleware is not a replacement for error handling, and error handling is not a replacement for middleware:

- middleware should not be used as the main mechanism for failure capture across publisher stages
- error handlers should not be used to implement normal event enrichment or routing behavior

## Registration

Use `UseErrorHandler(...)` on `EventPublisherBuilder`.

### Synchronous callback

```csharp
services.AddEventPublisher(options =>
    {
        options.Source = new Uri("https://orders.example.com");
        options.ThrowOnErrors = false;
    })
    .UseErrorHandler(context =>
    {
        Console.WriteLine(
            $"Publish failure at stage '{context.Stage}' " +
            $"for publisher '{context.PublisherName}': {context.Exception.Message}");
    })
    .AddRabbitMq(options => { /* transport config */ });
```

### Asynchronous callback

```csharp
services.AddEventPublisher()
    .UseErrorHandler(async context =>
    {
        await auditWriter.WriteAsync(new
        {
            context.PublisherName,
            context.Stage,
            Error = context.Exception.Message,
            EventType = context.Event?.Type,
            context.ChannelName
        });
    });
```

### Handler type

```csharp
services.AddEventPublisher()
    .UseErrorHandler<MyPublishErrorHandler>(ServiceLifetime.Scoped);
```

### Handler instance

```csharp
services.AddEventPublisher()
    .UseErrorHandler(new MyPublishErrorHandler());
```

## Implementing `IEventPublishErrorHandler`

```csharp
public sealed class MyPublishErrorHandler : IEventPublishErrorHandler
{
    public Task HandleAsync(EventPublishErrorContext context)
    {
        if (context.Stage == EventPublishStage.ChannelPublish)
        {
            // custom action for transport failures
        }

        return Task.CompletedTask;
    }
}
```

## What `EventPublishErrorContext` gives you

| Property | Purpose |
|----------|---------|
| `PublisherName` | Which publisher pipeline produced the failure |
| `Stage` | Where the failure happened |
| `Exception` | The original exception |
| `Event` | The `CloudEvent`, when available |
| `Options` | Effective publish options after unwrapping |
| `RawOptions` | Original publish options before wrapper unwrapping |
| `ChannelType` | Concrete channel type, when relevant |
| `ChannelName` | Logical channel name, when relevant |
| `DataType` | Original CLR data type, when event creation/conversion failed |
| `Data` | Original data object, when available |
| `Services` | Current scoped service provider |
| `CancellationToken` | Cancellation token for the publish operation |

## Typical use cases

- structured logging and diagnostics
- metrics and tracing
- custom audit records
- notifications to operators
- selective persistence of failed publish attempts
- custom retry scheduling outside of dead-letter replay

## Named publisher isolation

Error handlers are registered per publisher pipeline, just like channels.

```csharp
services.AddEventPublisher("alpha", builder => builder
    .UseErrorHandler(context => alphaRecorder.Add(context))
    .AddRabbitMq(options => { /* ... */ }));

services.AddEventPublisher("beta", builder => builder
    .UseErrorHandler(context => betaRecorder.Add(context))
    .AddRabbitMq(options => { /* ... */ }));
```

The `alpha` handler does not receive failures from `beta`, and vice versa.

## Relationship to `ThrowOnErrors`

Error handlers observe failures, but they do not replace the publisher's normal exception behavior.

- if `ThrowOnErrors` is `true`, publish failures still surface as `EventPublishException`
- if `ThrowOnErrors` is `false`, the publisher can continue after invoking handlers
- if the error handler itself throws, the publisher raises an `EventPublishException` wrapping both the original failure and the handler failure

## Guidelines for extending it

1. Keep handlers focused. A handler should have one clear responsibility: log, persist, alert, or translate.
2. Use `Stage` first. Different stages expose different context; branch early instead of assuming `Event` or `ChannelType` is always present.
3. Do not assume transport-only failures. `EventCreation` and `EventConversion` happen before channel dispatch.
4. Treat `Event`, `ChannelName`, `ChannelType`, `Data`, and `DataType` as optional.
5. Avoid long blocking work in the handler. Prefer short async I/O or delegation to another service.
6. Throw only intentionally. If a handler throws, it changes publisher behavior by promoting the failure into an `EventPublishException`.
7. Prefer transport-native reliability features when they already exist. Use custom handlers for cross-cutting concerns or gaps in the transport.

## Dead-letter handling is built on this

`Hermodr.Publisher.DeadLetter` is an implementation of the generic publish error handling capability focused on `EventPublishStage.ChannelPublish`.

Use the generic error handler page when you need broad failure interception across publisher stages.

Use [Dead-Letter Handling and Replay](dead-letter.md) when you specifically want to capture failed channel deliveries for inspection, persistence, and replay.
