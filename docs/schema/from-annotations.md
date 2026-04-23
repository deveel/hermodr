# Schema from Annotations

If you already have a data-transfer class decorated with `[Event]` and standard `System.ComponentModel.DataAnnotations` attributes, you can derive an `EventSchema` from it automatically — no need to write a schema by hand.

## Prerequisites

Install both the annotations and schema packages:

```bash
dotnet add package Deveel.Events.Annotations
dotnet add package Deveel.Events.Schema
```

## Annotating your class

Apply `[Event]` at class level and standard data-annotation attributes on properties:

```csharp
using System.ComponentModel.DataAnnotations;
using Deveel.Events;

[Event("order.placed", "1.0", Description = "Raised when a customer places an order")]
public class OrderPlacedData
{
    [Required]
    public Guid OrderId { get; set; }

    [Required]
    [Range(0.01, 1_000_000.0)]
    public decimal Amount { get; set; }

    [Required]
    public string Currency { get; set; } = default!;

    public string? Notes { get; set; }
}
```

### Attribute mapping

| Data annotation | Schema constraint |
|-----------------|-------------------|
| `[Required]` | `required: true` |
| `[Range(min, max)]` | `min` / `max` constraints |
| `[AllowedValues(...)]` / custom enum | `enum` constraint |
| Nullable reference type (`T?`) | `nullable: true` |
| `[EventProperty(name)]` | Overrides the property name in the schema |

## Deriving a schema

### Static helper

```csharp
using Deveel.Events;

var schema = EventSchema.FromDataType<OrderPlacedData>();
```

### Via the injectable factory (preferred in DI scenarios)

```csharp
using Deveel.Events;

// Register schema services
builder.Services.AddEventPublisher(); // already registers IEventSchemaFactory when Schema is referenced

public class MyService
{
    private readonly IEventSchemaFactory _schemaFactory;

    public MyService(IEventSchemaFactory schemaFactory)
    {
        _schemaFactory = schemaFactory;
    }

    public IEventSchema GetOrderSchema()
        => _schemaFactory.CreateFromType<OrderPlacedData>();
}
```

### From a `Type` reference

```csharp
var schema = EventSchema.FromDataType(typeof(OrderPlacedData));
```

## Generated schema

For the `OrderPlacedData` class above, the derived schema is equivalent to:

```csharp
EventSchema.Build("order.placed")
    .WithVersion("1.0")
    .WithDescription("Raised when a customer places an order")
    .AddProperty("OrderId",   "guid",    p => p.Required())
    .AddProperty("Amount",    "money",   p => p.Required().WithRange<decimal>(0.01m, 1_000_000m))
    .AddProperty("Currency",  "string",  p => p.Required())
    .AddProperty("Notes",     "string",  p => p.Nullable())
    .Build();
```

## Exporting the derived schema

Once you have an `IEventSchema`, you can export it in any supported format:

```csharp
// JSON
var jsonWriter = new EventSchemaJsonWriter(new JsonWriterOptions { Indented = true });
await jsonWriter.WriteToAsync(stream, schema);

// YAML (requires Deveel.Events.Schema.Yaml)
var yamlWriter = new EventSchemaYamlWriter();
await yamlWriter.WriteToAsync(stream, schema);

// AsyncAPI (requires Deveel.Events.Schema.AsyncApi)
var asyncApiWriter = new EventSchemaAsyncApiWriter(AsyncApiFormat.Yaml);
await asyncApiWriter.WriteToAsync(stream, schema);
```

## Related pages

- [Fluent Builder](fluent-builder.md)
- [Export as JSON](export-json.md)
- [Export as YAML](export-yaml.md)
- [Export as AsyncAPI](export-asyncapi.md)
- [Validation](validation.md)

