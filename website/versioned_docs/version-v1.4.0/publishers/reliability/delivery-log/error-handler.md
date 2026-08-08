# Delivery Log Error Handler

The error handler implements `IEventPublishErrorHandler` and serves the pipeline's error path rather than the middleware path.

## When It Acts

The error handler only acts when:
- `context.Stage == EventPublishStage.ChannelPublish`
- `context.Event` is non-null

## What It Does

1. Writes a record with `Outcome = Failed`
2. Captures exception type name and message as `ErrorCode` and `ErrorMessage`
3. Sets `ElapsedTime = TimeSpan.Zero` (exact start time not available in error context)

## Registration

Use the error handler when `ThrowOnErrors = false` and you still want failures recorded:

```csharp
builder.Services
    .AddEventPublisher(options =>
    {
        options.ThrowOnErrors = false;  // Don't throw, but still record failures
    })
    .AddDeliveryLog(log => log
        .UseInMemory()
        .UseErrorHandler());  // Captures failures on error path
```

## Using Both Middleware and Error Handler

Use both when you want every attempt captured:
- **Middleware** covers the attempt itself (success or failure)
- **Error handler** covers the error pipeline processing

```csharp
builder.Services
    .AddEventPublisher()
    .AddDeliveryLog(log => log
        .UseInMemory()
        .UseErrorHandler());
```

## Related Pages

- [Delivery Log Overview](README.md)
- [Middleware](middleware.md)
- [Registration](registration.md)
