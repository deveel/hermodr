# Event Annotations

The `Deveel.Events.Annotations` package provides attributes that let you embed event metadata directly on your data-transfer classes, removing the need to hard-code event types or versions at the call site.

## Installation

```bash
dotnet add package Deveel.Events.Annotations
```

## `[Event]` attribute

Applied at class level.  Describes the event type, version, and other envelope defaults.

```csharp
using Deveel.Events;

[Event("order.placed", "1.0")]
public class OrderPlacedData
{
    public Guid OrderId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = default!;
}
```

### Constructor overloads

| Signature | When to use |
|-----------|-------------|
| `[Event("type", "version")]` | Most common — event type + semver string |
| `[Event("type", "https://schemas.example.com/order.placed")]` | Event type + absolute URI to the JSON Schema |

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `EventType` | `string` | Required. The `type` attribute of the CloudEvent. |
| `DataVersion` | `string?` | SemVer version string (set when the second argument is not a URI). |
| `DataSchema` | `Uri?` | Absolute URI to the schema (set when the second argument is a URI). |
| `Description` | `string?` | Human-readable description for documentation purposes. |
| `ContentType` | `string?` | MIME content type of the data payload (e.g. `"application/json"`). When not set, publishing uses `EventPublisherOptions.DefaultContentType` (default: `"application/cloudevents+json"`), and schema generation defaults to `"object"`. |

## `[EventProperty]` attribute

Applied at property or field level.  Marks a member as part of the event payload and optionally overrides its serialised name.

```csharp
[Event("order.placed", "1.0")]
public class OrderPlacedData
{
    [EventProperty("order_id")]
    public Guid OrderId { get; set; }

    [EventProperty("amount")]
    public decimal Amount { get; set; }
}
```

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Name` | `string?` | Overrides the property name used in the serialised payload. |
| `Description` | `string?` | Human-readable description. |
| `Version` | `string?` | The event version this property belongs to. |
| `Schema` | `Uri?` | URI of the schema for this property's data. |

## `[EventAttributes]` attribute

A base class for attributes that inject arbitrary CloudEvent extension attributes into the envelope.  This is the foundation for things like `[AmqpExchange]` and `[AmqpRoutingKey]` (see [AMQP Annotations](../amqp/README.md)).

```csharp
// Inject a custom CloudEvent extension attribute
[EventAttributes("x-tenant-id", "acme")]
[Event("order.placed", "1.0")]
public class OrderPlacedData { /* ... */ }
```

## Publishing from an annotated class

```csharp
var data = new OrderPlacedData
{
    OrderId  = Guid.NewGuid(),
    Amount   = 99.95m,
    Currency = "USD"
};

// The IEventFactory service reads [Event] and builds the CloudEvent automatically.
await publisher.PublishAsync(data);
```

## Combining with Data Annotations

Standard `System.ComponentModel.DataAnnotations` attributes (e.g. `[Required]`, `[Range]`) placed on the same class are recognised by the Schema package when generating event schemas:

```csharp
using System.ComponentModel.DataAnnotations;
using Deveel.Events;

[Event("order.placed", "1.0", Description = "Raised when a customer places an order")]
public class OrderPlacedData
{
    [Required]
    [EventProperty("order_id")]
    public Guid OrderId { get; set; }

    [Required]
    [Range(0.01, 1_000_000.0)]
    [EventProperty("amount")]
    public decimal Amount { get; set; }

    [Required]
    [EventProperty("currency")]
    public string Currency { get; set; } = default!;

    public string? Notes { get; set; }
}
```

→ See [Schema from Annotations](../schema/from-annotations.md) for how to derive a schema from this class.

## Related pages

- [Event Publisher](event-publisher.md)
- [RabbitMQ Channel — AMQP Annotations](../publishers/rabbitmq.md#amqp-annotations)
- [Schema from Annotations](../schema/from-annotations.md)

---

## Assembly-level generator attributes

When `Deveel.Events.Generators` is referenced as an analyzer, two optional
**assembly-level** attributes let you supply defaults that are baked directly
into the generated code at build time, eliminating runtime lookups entirely.

### `[EventDataSchemaUri]`

Tells the generator the base URI to use when building the `dataschema` CloudEvents
attribute for events that declare a `DataVersion` (rather than a full absolute URI):

```csharp
[assembly: EventDataSchemaUri("https://schemas.example.com/events")]
```

The generator appends `/{eventType}/{dataVersion}` and emits the result as a
`const string` inside the generated `partial class`.  For example, given:

```csharp
[Event("order.placed", "2.0")]
public partial class OrderPlaced { ... }
```

the generator emits:

```csharp
private const string __dataSchema = "https://schemas.example.com/events/order.placed/2.0";
```

No runtime lookup via `EventGeneratorContext` is needed for the schema URI.

If the attribute is absent, the generator falls back to reading
`EventGeneratorContext.DataSchemaBaseUri` at call time (seeded from the active
`EventPublisher` instance's `EventPublisherOptions.DataSchemaBaseUri`).

### `[EventJsonSerializationOptions]`

Designates a type whose static `GetOptions()` method provides the
`JsonSerializerOptions` used when serialising event data inside `ToCloudEvent()`:

```csharp
[assembly: EventJsonSerializationOptions(typeof(MyApp.MyJsonOptions))]
```

The referenced type must expose:

```csharp
public static JsonSerializerOptions GetOptions();
```

The generator emits a direct static call to that method:

```csharp
Data = JsonSerializer.Serialize(this, global::MyApp.MyJsonOptions.GetOptions()),
```

If the attribute is absent, the generator reads `EventGeneratorContext.JsonSerializerOptions`
at runtime instead.

Both attributes are **independent** — you can use one, both, or neither.
