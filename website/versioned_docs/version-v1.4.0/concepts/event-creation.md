# Event Creation

This page explains how `EventPublisher` constructs a `CloudEvent` from application data
before the event enters the publish pipeline.  Two paths exist:

1. **From an annotated data class** — the publisher calls `IEventFactory` to build the
   `CloudEvent` from CLR annotations (`[Event]`, `[EventData]`, etc.).
2. **From a raw `CloudEvent`** — the caller supplies a fully or partially formed
   `CloudEvent` directly; the publisher only enriches it (fills `id`, `time`, `source`,
   and global attributes) before passing it through the pipeline.

After creation, every event follows the same enrich → middleware → validate → dispatch
pipeline regardless of how it was created.  See [Publish Pipeline & Middleware](publish-pipeline.md)
for the pipeline details.

---

## From an annotated data class

### Event annotations

Annotate a plain CLR class with `[Event]` to declare its CloudEvents metadata:

```csharp
using Hermodr;

[Event("com.example.order.placed")]
public class OrderPlaced
{
    public string OrderId { get; init; } = "";
    public decimal Total  { get; init; }
}
```

`IEventFactory` (registered automatically by `AddEventPublisher()`) reads these annotations
at runtime to construct a valid `CloudEvent`, mapping:

| Annotation | CloudEvent attribute |
|------------|---------------------|
| `[Event("type")]` | `type` |
| `[EventSource("uri")]` | `source` (overrides the global option) |
| `[EventDataSchema("uri")]` | `dataschema` |
| `[EventSubject("expr")]` | `subject` |
| `[EventContentType("mime")]` | `datacontenttype` |

The data object itself is serialised as the `CloudEvent.Data` payload using the configured
`JsonSerializerOptions`.

See [Event Annotations](event-annotations.md) for the full annotation reference.

### `PublishAsync<TEvent>` — generic overload

The generic overload is the most ergonomic path for strongly-typed application code:

```csharp
await publisher.PublishAsync(new OrderPlaced { OrderId = "ord-1", Total = 49.99m });
```

Internally it calls `CreateEventFromData(typeof(TEvent), data)`, which delegates to
`IEventFactory.CreateEventAsync(type, data)`.  The resulting `CloudEvent` then flows
through the normal pipeline.

If `TData` implements `IEventConvertible`, `PublishAsync<TData>` calls `ToCloudEvent()`
directly instead of going through `IEventFactory`, so the data object can take full
control of `CloudEvent` construction.

### `PublishAsync(Type, object?)` — non-generic overload

When the event type is only known at runtime (for example, in a reflection-based dispatch
layer), use the non-generic overload:

```csharp
Type eventType  = typeof(OrderPlaced);
object eventData = new OrderPlaced { OrderId = "ord-1", Total = 49.99m };

await publisher.PublishAsync(eventType, eventData);
```

This is equivalent to the generic overload but avoids a generic type parameter.

---

## From a raw `CloudEvent`

When you already have a `CloudEvent` instance (e.g. received from an external system or
built manually), pass it directly to `PublishEventAsync`:

```csharp
var @event = new CloudEvent
{
    Type   = "com.example.order.placed",
    Source = new Uri("https://myapp.example.com"),
    Data   = JsonSerializer.SerializeToElement(payload)
};

await publisher.PublishEventAsync(@event);
```

The publisher enriches the event (filling `id`, `time`, `source`, and global attributes
from `EventPublisherOptions`) and then runs the same middleware → validate → dispatch
pipeline as for annotation-driven events.

---

## `IEventFactory`

`IEventFactory` is the service responsible for converting a CLR data object into a
`CloudEvent`.  It is registered automatically by `AddEventPublisher()`.

| Member | Description |
|--------|-------------|
| `CreateEventAsync(Type type, object? data, CancellationToken ct)` | Builds a `CloudEvent` from `data` using the annotations on `type`. |

You can replace the default implementation with a custom one via DI:

```csharp
builder.Services.AddSingleton<IEventFactory, MyCustomEventFactory>();
```

Place this registration **after** `AddEventPublisher()` so it overrides the default.

### `IEventConvertible`

If you want full control over `CloudEvent` construction without replacing `IEventFactory`
globally, implement `IEventConvertible` on the data class:

```csharp
public class OrderPlaced : IEventConvertible
{
    public string OrderId { get; init; } = "";

    public CloudEvent ToCloudEvent()
    {
        return new CloudEvent
        {
            Type   = "com.example.order.placed",
            Source = new Uri("https://orders.example.com"),
            Id     = Guid.NewGuid().ToString(),
            Data   = JsonSerializer.SerializeToElement(this)
        };
    }
}
```

When `PublishAsync<TData>` detects that `TData` implements `IEventConvertible`, it calls
`ToCloudEvent()` directly and bypasses `IEventFactory` entirely.

---

## `CreateEventFromData` — protected virtual hook

`EventPublisher` exposes `CreateEventFromData(Type, object?)` as a `protected virtual`
method so subclasses can customise how annotated data classes become `CloudEvent`s without
replacing `IEventFactory`:

```csharp
public class EnrichedPublisher : EventPublisher
{
    protected override CloudEvent CreateEventFromData(Type type, object? data)
    {
        var @event = base.CreateEventFromData(type, data);

        // Stamp a custom extension attribute on every factory-created event
        var attr = CloudEventAttribute.CreateExtension("region", CloudEventAttributeType.String);
        @event[attr] = "eu-west-1";

        return @event;
    }
}
```

Register the subclass with `UsePublisher<T>()` on the builder:

```csharp
builder.Services
    .AddEventPublisher()
    .UsePublisher<EnrichedPublisher>();
```

---

## Publishing to a named channel

Both creation paths support targeting a specific channel by name via the `channelName`
convenience overloads:

```csharp
// Annotation-driven
await publisher.PublishAsync(new OrderPlaced { /* … */ }, channelName: "rabbit-orders");

// Raw CloudEvent
await publisher.PublishEventAsync(@event, channelName: "rabbit-notifications");
```

Channels that have no name always receive every event regardless of any filter.  See
[Named Channels](../publishers/named-channels.md) for the full guide.

---

## Related pages

- [Event Publisher](event-publisher.md)
- [Publish Pipeline & Middleware](publish-pipeline.md)
- [Event Annotations](event-annotations.md)
- [Publish Channels](publish-channels.md)
- [Named Channels](../publishers/named-channels.md)

