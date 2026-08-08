# Schema Validation at Publish Time

The `Hermodr.Publisher.EventSchema` package adds a middleware component that automatically validates every event's payload against its registered schema before the event reaches any publish channel.

## Prerequisites

```bash
dotnet add package Hermodr.Publisher.EventSchema
```

## Quick start

Register the schema validation middleware on the publisher builder:

```csharp
using Hermodr;

builder.Services
    .AddEventPublisher(opts => opts.Source = new Uri("https://example.com"))
    .UseSchemaValidation()
    .AddChannel<RabbitMqPublishChannel>();
```

`UseSchemaValidation()` auto-registers all schema services (`IEventSchemaRegistry`, `IEventSchemaValidator`, deserializers) if they are not already registered.

## Registering schemas

Schemas must be registered before validation can run. Pass them via the `configureSchemaServices` callback:

```csharp
builder.Services
    .AddEventPublisher()
    .UseSchemaValidation(
        configureOptions: null,
        configureSchemaServices: schema =>
        {
            schema.SchemaRegistrations.Add(registry =>
            {
                var schema = new EventSchema("order.placed", "1.0", "application/json");
                schema.Properties.Add(new EventProperty("orderId", "string", "1.0"));
                schema.Properties[0]!.Constraints.Add(new PropertyRequiredConstraint());
                schema.Properties.Add(new EventProperty("amount", "decimal", "1.0"));
                schema.Properties[1]!.Constraints.Add(new PropertyRequiredConstraint());
                registry.Register(schema);
            });
        });
```

You can also register schemas from CLR types using assembly scanning:

```csharp
schema.AssembliesToScan.Add(typeof(OrderPlacedData).Assembly);
```

## Configuration

### Missing schema behavior

Controls what happens when no schema is registered for an event type:

| Behavior | Description |
|----------|-------------|
| `Allow` (default) | The event is published without validation |
| `Block` | A `SchemaValidationException` is thrown |
| `Fallback` | A schema with no constraints is used (all properties pass) |

```csharp
.UseSchemaValidation(opts =>
{
    opts.MissingSchemaBehavior = MissingSchemaBehavior.Block;
})
```

### Validation failure behavior

Controls what happens when the payload does not match the schema:

| Behavior | Description |
|----------|-------------|
| `Throw` (default) | A `SchemaValidationException` is thrown with all validation errors |
| `Log` | Errors are logged but the event is still published |

```csharp
.UseSchemaValidation(opts =>
{
    opts.ValidationFailureBehavior = ValidationFailureBehavior.Log;
})
```

## Version-aware validation

The middleware supports schema versioning through the `dataversion` CloudEvent extension attribute. When present, the exact version is looked up; when absent, the latest registered version is used.

```csharp
var @event = new CloudEvent
{
    Type = "order.placed",
    Source = new Uri("https://example.com"),
    Data = data
};

// Set the version extension attribute
var attr = CloudEventAttribute.CreateExtension("dataversion", CloudEventAttributeType.String);
@event[attr] = "2.0";
```

## How it works

The middleware runs after enrichment and before channel dispatch:

1. Looks up the schema by `CloudEvent.Type` (and optional `dataversion` extension)
2. Deserializes the event payload using a format-agnostic deserializer (JSON, XML, etc.)
3. Validates every property against the schema's constraints (required, range, enum)
4. On failure: throws or logs according to `ValidationFailureBehavior`
5. On success: calls `next(context)` to continue the pipeline

## Related pages

- [Schema Validation](validation.md) — manual validation with `IEventSchemaValidator`
- [Fluent Builder](fluent-builder.md) — building schemas programmatically
- [From Annotations](from-annotations.md) — deriving schemas from annotated classes
- [Publish Pipeline](../concepts/publish-pipeline.md) — middleware pipeline overview
