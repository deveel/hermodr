# Event Publisher

The `IEventPublisher` interface is the single entry point for publishing events in your application.

## Registration

Register the publisher in your DI container using the `AddEventPublisher` extension method:

```csharp
using Deveel.Events;

// Minimal registration
builder.Services.AddEventPublisher();

// With inline options
builder.Services.AddEventPublisher(options =>
{
    options.Source = new Uri("https://myapp.example.com");
    options.ThrowOnErrors = true;
});

// Bind options from appsettings.json
builder.Services.AddEventPublisher("Events:Publisher");
```

`AddEventPublisher` returns an `EventPublisherBuilder` that you chain to register channels and other services.

## `EventPublisherOptions`

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Source` | `Uri?` | `null` | Default `source` attribute of every `CloudEvent`. Recommended to set globally. |
| `ThrowOnErrors` | `bool` | `false` | Re-throws exceptions from channels instead of swallowing them. |
| `JsonSerializerOptions` | `JsonSerializerOptions?` | default | Options used when serialising JSON payloads. |
| `Attributes` | `Dictionary<string, object?>` | `{}` | Extra CloudEvent attributes added to every event. |
| `DataSchemaBaseUri` | `Uri?` | `null` | Base URI prepended to the event type to form the `dataschema` when none is specified on the event. |
| `DefaultContentType` | `string?` | `"application/cloudevents+json"` | Default MIME content type used when not specified on the event. |

### Example `appsettings.json`

```json
{
  "Events": {
    "Publisher": {
      "Source": "https://myapp.example.com",
      "ThrowOnErrors": true,
      "DataSchemaBaseUri": "https://schemas.myapp.example.com/"
    }
  }
}
```

## Publishing events

### From an annotated data class

```csharp
public class OrderService
{
    private readonly IEventPublisher _publisher;

    public OrderService(IEventPublisher publisher) => _publisher = publisher;

    public async Task PlaceOrderAsync(OrderPlacedData data)
    {
        // IEventCreator reads [Event] on OrderPlacedData and builds the CloudEvent
        await _publisher.PublishAsync(data);
    }
}
```

### From a raw `CloudEvent`

```csharp
var @event = new CloudEvent
{
    Type   = "order.placed",
    Source = new Uri("https://myapp.example.com"),
    Data   = orderPayload
};

await publisher.PublishEventAsync(@event);
```

### Fan-out behaviour

Every call to `PublishEventAsync` dispatches the event to **all** registered `IEventPublishChannel` instances, one by one.  If `ThrowOnErrors` is `false` (the default), a failing channel is logged but does not prevent delivery to the remaining channels.

## Extensibility

### Custom publisher

Replace the default `EventPublisher` with your own implementation:

```csharp
builder.Services
    .AddEventPublisher()
    .UsePublisher<MyCustomPublisher>();
```

### Custom ID generator

```csharp
builder.Services
    .AddEventPublisher()
    .UseGuid("N");   // compact format without dashes
```

### Custom system time

Useful in tests to control event timestamps:

```csharp
builder.Services
    .AddEventPublisher()
    .UseSystemTime<FrozenSystemTime>();
```

`FrozenSystemTime` is any class implementing `IEventSystemTime`:

```csharp
public class FrozenSystemTime : IEventSystemTime
{
    public DateTimeOffset UtcNow { get; } = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
}
```

## Related pages

- [Publish Channels](publish-channels.md)
- [Event Annotations](event-annotations.md)

