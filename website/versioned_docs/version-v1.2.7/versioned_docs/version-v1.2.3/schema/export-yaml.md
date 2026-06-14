# Export Schema as YAML

`EventSchemaYamlWriter` serialises an `IEventSchema` to a YAML stream using [YamlDotNet](https://github.com/aaubry/YamlDotNet).

## Installation

```bash
dotnet add package Deveel.Events.Schema.Yaml
```

## Usage

```csharp
using Deveel.Events;

var schema = EventSchema.FromDataType<OrderPlacedData>();

var writer = new EventSchemaYamlWriter();   // camelCase property names by default

await using var stream = File.OpenWrite("order-placed-schema.yaml");
await writer.WriteToAsync(stream, schema);
```

## Output format

```yaml
type: order.placed
version: "1.0"
contentType: object
description: Raised when a customer places an order
properties:
  orderId:
    dataType: guid
    required: true
  amount:
    dataType: money
    required: true
    min: 0.01
    max: 1000000
  currency:
    dataType: string
    required: true
  notes:
    dataType: string
    nullable: true
```

Property names are converted to **camelCase** by default.

## Custom serialiser

Supply a custom `YamlDotNet.Serialization.ISerializer` to control naming conventions, anchors, and other output options:

```csharp
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

var serializer = new SerializerBuilder()
    .WithNamingConvention(UnderscoredNamingConvention.Instance)
    .Build();

var writer = new EventSchemaYamlWriter(serializer);
```

## Writing to a string

```csharp
await using var memStream = new MemoryStream();
await writer.WriteToAsync(memStream, schema);
var yaml = Encoding.UTF8.GetString(memStream.ToArray());
```

## Related pages

- [Export as JSON](export-json.md)
- [Export as AsyncAPI](export-asyncapi.md)
- [Fluent Builder](fluent-builder.md)
- [From Annotations](from-annotations.md)

