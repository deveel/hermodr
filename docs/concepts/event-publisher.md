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

## Validation

Before an event is dispatched to any channel the publisher checks that the four **required** CloudEvents attributes are present and non-empty.  This guard runs **after** the enrichment steps (id generation, timestamp, source from options, extra attributes), so values that are automatically filled in — such as the auto-generated `id` or a `source` pulled from `EventPublisherOptions` — count towards satisfying the requirement.

| Attribute checked | Auto-filled by publisher? | Must be provided by caller if… |
|---|---|---|
| `id` | ✅ via `IEventIdGenerator` | Never (always generated if absent) |
| `source` | ✅ if `EventPublisherOptions.Source` is set | Options source is `null` and caller did not set it |
| `type` | ❌ | Always (no default exists) |
| `specversion` | ✅ by the CloudNative SDK (always `"1.0"`) | Never in practice |

If any required attribute is still missing after enrichment, `PublishEventAsync` throws an `InvalidCloudEventException` **before** touching any channel, so brokers never receive a structurally invalid envelope.

### `InvalidCloudEventException`

`InvalidCloudEventException` is a subclass of `ArgumentException`.  It exposes a `MissingAttributes` property listing every attribute that failed the check, so you can report all problems in a single throw rather than one at a time.

```csharp
try
{
    await publisher.PublishEventAsync(incompleteEvent);
}
catch (InvalidCloudEventException ex)
{
    // ex.MissingAttributes → e.g. ["type", "source"]
    Console.WriteLine($"Invalid event: {ex.Message}");
}
```

### Overriding the validation logic

`ValidateCloudEvent` is a `protected virtual` method.  Subclass `EventPublisher` to tighten or relax the rules:

```csharp
public class StrictPublisher : EventPublisher
{
    public StrictPublisher(/* … */) : base(/* … */) { }

    protected override void ValidateCloudEvent(CloudEvent @event)
    {
        // Run the standard four-attribute check first
        base.ValidateCloudEvent(@event);

        // Then add application-specific rules
        if (@event.Subject == null)
            throw new InvalidCloudEventException(["subject"]);
    }
}
```

> **Note:** Full payload schema validation (checking the `data` field against its declared schema) is a separate, deferred feature targeted at v1.3.0.  See [Schema Validation at Publish Time](../schema/validation.md) in the roadmap for details.

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

Every call to `PublishEventAsync` follows this sequence:

1. **Enrich** — fill in `id`, `time`, `source`, and publisher-level attributes where they are absent.
2. **Validate** — assert that the four required CloudEvents attributes (`id`, `source`, `type`, `specversion`) are present; throws `InvalidCloudEventException` if not.
3. **Dispatch** — send the event to **all** registered `IEventPublishChannel` instances, one by one.

If `ThrowOnErrors` is `false` (the default), a failing channel is logged but does not prevent delivery to the remaining channels.  `InvalidCloudEventException` is always thrown regardless of `ThrowOnErrors`, because an invalid envelope is a programming error rather than a transient delivery failure.

## Per-call publish options

Every `PublishAsync` and `PublishEventAsync` overload accepts an optional `EventPublishOptions?` parameter.  Passing a value lets you override channel-level defaults **for a single call** without changing the registered configuration.

### Single-channel override

Pass an instance of the concrete options type that matches the target channel.

```csharp
// Only the general RabbitMQ channel picks this up;
// every typed RabbitMQ channel and every other channel uses its defaults.
await publisher.PublishEventAsync(@event, new RabbitMqPublishOptions
{
    RoutingKey = "orders.priority",
    ExchangeName = "priority-exchange",
});
```

To override a **typed** channel, pass the corresponding typed options class:

```csharp
// Only RabbitMqEventPublishChannel<OrderPlaced> picks this up.
// The general RabbitMQ channel and all other channels use their defaults.
await publisher.PublishEventAsync(@event, new RabbitMqPublishOptions<OrderPlaced>
{
    RoutingKey = "orders.priority",
});
```

### Multi-channel override with `CombinedPublishOptions`

When more than one channel must receive different per-call overrides in the same call, wrap all the options in a `CombinedPublishOptions`:

```csharp
var overrides = new CombinedPublishOptions(
    // → general RabbitMQ channel
    new RabbitMqPublishOptions     { RoutingKey   = "orders.priority" },
    // → RabbitMQ channel typed for OrderPlaced
    new RabbitMqPublishOptions<OrderPlaced> { ExchangeName = "priority-orders" },
    // → general Webhook channel
    new WebhookPublishOptions      { EndpointUrl  = "https://partner.example.com/priority-hook" },
    // → Service Bus channel typed for OrderPlaced
    new ServiceBusPublishOptions<OrderPlaced> { QueueName = "priority-queue" });

await publisher.PublishEventAsync(@event, overrides);
```

The publisher inspects every registered channel in turn and extracts the **first compatible entry** from the bundle — matching general options to general channels and typed options to their corresponding typed channels.  Channels that have no compatible entry fall back silently to their registered defaults.

### Options resolution rules

The following table summarises how the publisher resolves the `options` parameter before forwarding it to each channel:

| `options` value passed | General channel receives | Typed channel `IEventPublishChannel<TEvent>` receives |
|---|---|---|
| `null` | `null` → uses its registered defaults | `null` → uses its registered defaults |
| A non-generic `XxxPublishOptions` instance | The override (if assignable to the channel's `TOptions`) | `null` → uses its defaults — general options are **not** forwarded to typed channels |
| A generic `XxxPublishOptions<TEvent>` instance | `null` → typed options are **not** forwarded to general channels | The override if `TEvent` matches the channel; `null` otherwise |
| `CombinedPublishOptions` | First non-typed bundled entry whose type is assignable to the channel's `TOptions`, or `null` | First bundled entry parameterised with this channel's `TEvent`, or `null` |

The discriminator between "general" and "typed" options is the **runtime generic type structure**: an options instance is considered *typed* when its actual type (or any type in its inheritance chain) is a closed generic type whose type arguments include the target event type.  Built-in typed options classes (`RabbitMqPublishOptions<TEvent>`, `ServiceBusPublishOptions<TEvent>`, etc.) satisfy this automatically.

> **Note:** Options resolution is handled by the `protected virtual ResolveChannelOptions(IEventPublishChannel, EventPublishOptions?)` method on `EventPublisher`.  Subclasses can override it to implement custom matching logic.

### `CombinedPublishOptions` API

| Member | Description |
|---|---|
| `CombinedPublishOptions(params EventPublishOptions[])` | Creates the bundle from a params array. Order is preserved; first match wins. |
| `CombinedPublishOptions(IEnumerable<EventPublishOptions>)` | Creates the bundle from any sequence. |
| `Options` | Read-only list of all bundled entries. |
| `GetOptions<TOptions>()` | Returns the first entry assignable to `TOptions`, or `null`. |
| `GetOptions(Type)` | Non-generic equivalent — useful when the options type is only known at runtime. |

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

