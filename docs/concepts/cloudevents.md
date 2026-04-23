# CloudEvents Standard

Deveel Events uses the [CloudEvents](https://cloudevents.io/) specification as its event model.  Every event published through the framework is represented as a `CloudEvent` object from the [`CloudNative.CloudEvents`](https://github.com/cloudevents/sdk-csharp) SDK.

## Why CloudEvents?

- **Interoperability** — CloudEvents is a CNCF specification adopted by Azure, Google Cloud, AWS, and many other platforms.  Events can be consumed by any system that understands the standard.
- **Schema-agnostic** — The envelope is fixed; the `data` payload can be any content type.
- **Tooling ecosystem** — Schema registries, event bridges, and observability tools often natively support CloudEvents.

## The CloudEvent Envelope

A `CloudEvent` consists of a set of **required** and **optional** attributes:

| Attribute | Type | Description |
|-----------|------|-------------|
| `id` | `string` | Unique identifier for this specific event occurrence |
| `source` | `Uri` | Identifies the context in which the event happened (e.g. your service URL) |
| `specversion` | `string` | Always `"1.0"` |
| `type` | `string` | Describes the kind of event (e.g. `"order.placed"`) |
| `datacontenttype` | `string?` | MIME type of the `data` payload (e.g. `"application/json"`) |
| `dataschema` | `Uri?` | URI of the schema that the `data` conforms to |
| `subject` | `string?` | Additional context about the subject of the event |
| `time` | `DateTimeOffset?` | Timestamp when the event occurred |
| `data` | `object?` | The event payload |

## How Deveel Events Populates the Envelope

When you call `publisher.PublishAsync(data)` with an annotated data object, the `IEventCreator` service reads the `[Event]` attribute and populates the envelope as follows:

| CloudEvent attribute | Source |
|----------------------|--------|
| `type` | `[Event]` attribute's `EventType` property |
| `id` | `IEventIdGenerator` (default: new GUID) |
| `source` | `EventPublisherOptions.Source` |
| `time` | `IEventSystemTime.UtcNow` |
| `dataschema` | `[Event]` attribute's `DataSchema`, or `EventPublisherOptions.DataSchemaBaseUri` + event type |
| `datacontenttype` | `[Event]` attribute's `ContentType` |
| `data` | The annotated object itself |

Any additional CloudEvent attributes declared via `[EventAttributes]` (or AMQP-specific attributes) are merged in.

## Working with CloudEvent Directly

You can bypass the annotation system and construct a `CloudEvent` manually when you need full control:

```csharp
using CloudNative.CloudEvents;

var @event = new CloudEvent
{
    Id      = Guid.NewGuid().ToString(),
    Type    = "com.acme.order.shipped",
    Source  = new Uri("https://orders.acme.com"),
    Time    = DateTimeOffset.UtcNow,
    DataContentType = "application/json",
    Data    = new { OrderId = 42, TrackingNumber = "1Z999AA1" }
};

await publisher.PublishEventAsync(@event);
```

## Further Reading

- [CloudEvents Specification v1.0](https://github.com/cloudevents/spec/blob/v1.0.2/cloudevents/spec.md)
- [CloudNative.CloudEvents SDK for .NET](https://github.com/cloudevents/sdk-csharp)

