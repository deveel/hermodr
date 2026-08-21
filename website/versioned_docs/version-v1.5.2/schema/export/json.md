# Export as JSON

JSON export serializes event schemas to JSON format, ideal for schema registries, validation libraries, and tooling integration.

## When to Use JSON Export

- ✅ Integrating with schema registries
- ✅ Using JSON Schema validation libraries
- ✅ API gateways that consume JSON Schema
- ✅ Automated CI/CD validation pipelines
- ✅ Programmatic schema comparison

## Installation

```bash
dotnet add package Hermodr.Schema
```

## Basic Usage

### Export to Stream

```csharp
using Hermodr;
using System.Text.Json;

var schema = EventSchema.FromDataType<OrderPlacedData>();

var jsonWriter = new EventSchemaJsonWriter(new JsonWriterOptions { Indented = true });

using var stream = new MemoryStream();
await jsonWriter.WriteToAsync(stream, schema);

stream.Position = 0;
var json = await new StreamReader(stream).ReadToEndAsync();
Console.WriteLine(json);
```

### Export to File

```csharp
var schema = EventSchema.FromDataType<OrderPlacedData>();
var jsonWriter = new EventSchemaJsonWriter(new JsonWriterOptions { Indented = true });

using var fileStream = File.Create("order-placed.schema.json");
await jsonWriter.WriteToAsync(fileStream, schema);
```

### Export to String

```csharp
var schema = EventSchema.FromDataType<OrderPlacedData>();
var jsonWriter = new EventSchemaJsonWriter(new JsonWriterOptions { Indented = true });

using var stream = new MemoryStream();
await jsonWriter.WriteToAsync(stream, schema);

stream.Position = 0;
var json = await new StreamReader(stream).ReadToEndAsync();
```

## Output Format

### Simple Schema

For a simple schema without constraints:

```csharp
var schema = EventSchema.Build("user.created")
    .WithVersion("1.0")
    .AddProperty("userId", "guid", p => p.Required())
    .AddProperty("email", "string", p => p.Required())
    .Build();
```

Produces:

```json
{
  "type": "user.created",
  "version": "1.0",
  "contentType": "application/json",
  "properties": [
    {
      "name": "userId",
      "dataType": "guid",
      "required": true
    },
    {
      "name": "email",
      "dataType": "string",
      "required": true
    }
  ]
}
```

### Schema with Constraints

For schemas with range and enum constraints:

```csharp
var schema = EventSchema.Build("order.placed")
    .WithVersion("1.0")
    .AddProperty("amount", "money", p => p
        .Required()
        .WithRange<decimal>(0.01m, 1_000_000m))
    .AddProperty("currency", "string", p => p
        .Required()
        .WithAllowedValues<string>(["USD", "EUR", "GBP"]))
    .Build();
```

Produces:

```json
{
  "type": "order.placed",
  "version": "1.0",
  "contentType": "application/json",
  "properties": [
    {
      "name": "amount",
      "dataType": "money",
      "required": true,
      "constraints": {
        "min": 0.01,
        "max": 1000000.0
      }
    },
    {
      "name": "currency",
      "dataType": "string",
      "required": true,
      "constraints": {
        "enum": ["USD", "EUR", "GBP"]
      }
    }
  ]
}
```

## Writer Options

### Indented Output (Pretty-Print)

```csharp
var jsonWriter = new EventSchemaJsonWriter(new JsonWriterOptions
{
    Indented = true,
    IndentCharacter = ' ',
    IndentSize = 2
});
```

### Compact Output (Minified)

```csharp
var jsonWriter = new EventSchemaJsonWriter(new JsonWriterOptions
{
    Indented = false,
    SkipValidation = true
});
```

## Integration Examples

### Schema Registry Upload

```csharp
public class SchemaRegistryClient
{
    private readonly HttpClient _http;

    public async Task RegisterSchemaAsync(IEventSchema schema)
    {
        var jsonWriter = new EventSchemaJsonWriter();
        using var stream = new MemoryStream();
        await jsonWriter.WriteToAsync(stream, schema);
        
        stream.Position = 0;
        var content = new StreamContent(stream);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        
        await _http.PostAsync("/api/schemas", content);
    }
}
```

### CI/CD Validation

```yaml
# .github/workflows/validate-schemas.yml
name: Validate Schemas

on: [push]

jobs:
  validate:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      
      - name: Export schemas
        run: dotnet run -- ExportSchemas
      
      - name: Validate JSON schemas
        run: |
          for file in schemas/*.json; do
            cat "$file" | jq . > /dev/null || exit 1
          done
```

## Related Pages

- [Export Overview](README.md)
- [Export as YAML](yaml.md)
- [Export as AsyncAPI](asyncapi.md)
