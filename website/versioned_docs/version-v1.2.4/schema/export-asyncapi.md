# Export Schema as AsyncAPI

The `Hermodr.Schema.AsyncApi` package generates fully valid [AsyncAPI 2.x](https://www.asyncapi.com/) documents from one or more event schemas.

## Installation

```bash
dotnet add package Hermodr.Schema.AsyncApi
```

## Single schema → standalone document

`EventSchemaAsyncApiWriter` wraps a single `IEventSchema` in a complete AsyncAPI 2.x document.  It:

- Declares the schema under `components/schemas`
- Creates a matching message under `components/messages`
- Wires a `subscribe` channel that references the message

### JSON output (default)

```csharp
using Hermodr;

var schema = EventSchema.FromDataType<OrderPlacedData>();

var writer = new EventSchemaAsyncApiWriter(
    format: AsyncApiFormat.Json,
    title: "Order Events",
    documentVersion: "1.0");

await using var stream = File.OpenWrite("order-placed-asyncapi.json");
await writer.WriteToAsync(stream, schema);
```

### YAML output

```csharp
var writer = new EventSchemaAsyncApiWriter(AsyncApiFormat.Yaml);

await using var stream = File.OpenWrite("order-placed-asyncapi.yaml");
await writer.WriteToAsync(stream, schema);
```

## Multiple schemas → combined document

`EventSchemasAsyncApiWriter` merges several schemas into one document — useful for generating a service-wide contract file.

```csharp
using Hermodr;

IEnumerable<IEventSchema> schemas =
[
    EventSchema.FromDataType<OrderPlacedData>(),
    EventSchema.FromDataType<OrderCancelledData>(),
    EventSchema.FromDataType<UserRegisteredData>()
];

var writer = new EventSchemasAsyncApiWriter(
    title: "My Service Events",
    version: "2.0",
    format: AsyncApiFormat.Yaml);

await using var stream = File.OpenWrite("events-asyncapi.yaml");
await writer.WriteToAsync(stream, schemas);
```

## `AsyncApiFormat`

| Value | Output |
|-------|--------|
| `AsyncApiFormat.Json` | JSON document |
| `AsyncApiFormat.Yaml` | YAML document |

## Low-level extension helpers

`EventSchemaAsyncApiExtensions` exposes helpers for fine-grained control:

```csharp
using Hermodr;
using NJsonSchema;
using Saunter.AsyncApiSchema.v2;

var schema = EventSchema.FromDataType<OrderPlacedData>();

// Convert to NJsonSchema
JsonSchema jsonSchema = schema.ToJsonSchema();

// Convert to an AsyncAPI Message object
Message message = schema.ToAsyncApiMessage();

// Build a standalone AsyncApiDocument
AsyncApiDocument document = schema.ToAsyncApiDocument(
    title: "Order Events",
    version: "1.0");

// Or add the schema to an existing AsyncApiDocument
var existingDoc = new AsyncApiDocument { /* ... */ };
existingDoc.AddSchema(schema);
```

## Example YAML output

```yaml
asyncapi: 2.6.0
info:
  title: My Service Events
  version: "2.0"
channels:
  order/placed:
    subscribe:
      message:
        $ref: "#/components/messages/OrderPlaced"
  order/cancelled:
    subscribe:
      message:
        $ref: "#/components/messages/OrderCancelled"
components:
  schemas:
    OrderPlacedData:
      type: object
      required:
        - orderId
        - amount
        - currency
      properties:
        orderId:
          type: string
          format: uuid
        amount:
          type: number
          minimum: 0.01
          maximum: 1000000
        currency:
          type: string
        notes:
          type: string
          nullable: true
  messages:
    OrderPlaced:
      payload:
        $ref: "#/components/schemas/OrderPlacedData"
      name: order.placed
      title: Order Placed
```

## Related pages

- [Export as JSON](export-json.md)
- [Export as YAML](export-yaml.md)
- [From Annotations](from-annotations.md)
- [Fluent Builder](fluent-builder.md)

