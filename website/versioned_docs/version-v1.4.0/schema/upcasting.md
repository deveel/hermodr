# Event Upcasting

When an event schema evolves, older stored or inbound events may still be encoded with a previous version. Upcasting transforms those legacy payloads to the latest schema version before they are handled or republished.

The upcasting pipeline lives in `Hermodr.Schema`. The publisher integration is in `Hermodr.Publisher.EventSchema` via `UseEventUpcasting()`.

## How it works

1. The middleware reads the event's current schema version from the `dataversion` extension attribute, or from the last segment of the `dataschema` URI.
2. It looks up the latest registered schema for the event type.
3. If the event is behind, it runs the registered upcasting pipeline to migrate the payload through one or more `IEventUpcaster` steps.
4. The migrated payload, `dataversion`, and optionally `dataschema` are rewritten on the CloudEvent before it continues down the publisher pipeline.

## Writing an upcaster

Implement `IEventUpcaster` and register it in DI:

```csharp
public sealed class OrderPlacedV1ToV2Upcaster : IEventUpcaster
{
    public bool CanUpcast(string eventType, Version fromVersion, Version toVersion)
        => eventType == "order.placed"
        && fromVersion == new Version(1, 0)
        && toVersion == new Version(2, 0);

    public Task<JsonNode> UpcastAsync(UpcastContext context, CancellationToken cancellationToken = default)
    {
        if (context.Data is JsonObject obj)
        {
            // V2 split the single "customer" string into "customerId" and "customerName"
            if (obj["customer"] is { } oldValue)
            {
                var parts = oldValue.GetValue<string>().Split(':');
                obj["customerId"] = parts[0];
                obj["customerName"] = parts.Length > 1 ? parts[1] : "";
                obj.Remove("customer");
            }
        }

        return Task.FromResult(context.Data);
    }
}
```

Register the upcaster and enable upcasting in the publisher pipeline:

```csharp
services.AddEventPublisher()
    .UseEventUpcasting()
    .AddTestChannel(e => received.Add(e));

services.AddSingleton<IEventUpcaster, OrderPlacedV1ToV2Upcaster>();
```

> Register `IEventUpcaster` instances through the normal DI container. They are discovered automatically by `EventUpcasterRegistry`. You can also register them manually via `IEventUpcasterRegistry.Register(upcaster)`.

## Chaining upcasters

The pipeline discovers intermediate schema versions from the registered `IEventSchemaRegistry`. If you have schemas for `1.0`, `2.0`, and `3.0`, and upcasters for `1.0 → 2.0` and `2.0 → 3.0`, an event at version `1.0` is migrated through both steps automatically.

Direct jumps are also supported: if an upcaster declares `1.0 → 3.0`, the pipeline uses it instead of chaining.

## Missing upcaster behaviour

Configure `EventUpcastingOptions` to decide what happens when no upcaster chain can be built:

```csharp
services.AddEventPublisher()
    .UseEventUpcasting(options =>
    {
        options.MissingUpcasterBehavior = MissingUpcasterBehavior.Throw;
    });
```

| Behaviour | Effect |
|-----------|--------|
| `PassThrough` (default) | The event is published unchanged. |
| `Throw` | An `EventUpcastingException` is thrown. |

## Content type adapters

The built-in `JsonEventUpcastingDataAdapter` handles `application/json` and CloudEvents JSON content types. Future adapters can implement `IEventUpcastingDataAdapter` for XML, Protobuf, or other formats without changing the core pipeline.

## Order in the publisher pipeline

Register upcasting **before** schema validation so that migrated events validate against the latest schema:

```csharp
services.AddEventPublisher()
    .UseEventUpcasting()
    .UseSchemaValidation()
    .AddRabbitMqChannel(...);
```

## Version sources

The middleware uses the first available source:

1. The `dataversion` CloudEvents extension attribute.
2. The last path segment of the `dataschema` URI.

If neither is present, the event is passed through unchanged.
