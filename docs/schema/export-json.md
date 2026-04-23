# Export Schema as JSON

`EventSchemaJsonWriter` serialises an `IEventSchema` to a JSON stream.  It is included in the `Deveel.Events.Schema` package — no extra installation required.

## Usage

```csharp
using Deveel.Events;
using System.Text.Json;

var schema = EventSchema.FromDataType<OrderPlacedData>();

var writer = new EventSchemaJsonWriter(new JsonWriterOptions { Indented = true });

await using var stream = File.OpenWrite("order-placed-schema.json");
await writer.WriteToAsync(stream, schema);
```

## `JsonWriterOptions`

Pass a `System.Text.Json.JsonWriterOptions` instance to control formatting:

| Option | Default | Effect |
|--------|---------|--------|
| `Indented` | `false` | Pretty-print the JSON output |
| `Encoder` | `null` | Custom character encoder |

## Output format

```json
{
  "type": "order.placed",
  "version": "1.0",
  "contentType": "object",
  "description": "Raised when a customer places an order",
  "properties": {
    "OrderId": {
      "dataType": "guid",
      "required": true
    },
    "Amount": {
      "dataType": "money",
      "required": true,
      "min": 0.01,
      "max": 1000000
    },
    "Currency": {
      "dataType": "string",
      "required": true
    },
    "Notes": {
      "dataType": "string",
      "nullable": true
    }
  }
}
```

## Writing to a string

```csharp
await using var memStream = new MemoryStream();
await writer.WriteToAsync(memStream, schema);
var json = Encoding.UTF8.GetString(memStream.ToArray());
```

## `IEventSchemaWriter` interface

`EventSchemaJsonWriter` implements `IEventSchemaWriter`:

```csharp
public interface IEventSchemaWriter
{
    Task WriteToAsync(Stream stream, IEventSchema schema, CancellationToken cancellationToken = default);
}
```

You can register and resolve it via DI if you prefer not to instantiate it directly.

## Related pages

- [Export as YAML](export-yaml.md)
- [Export as AsyncAPI](export-asyncapi.md)
- [Fluent Builder](fluent-builder.md)
- [From Annotations](from-annotations.md)

