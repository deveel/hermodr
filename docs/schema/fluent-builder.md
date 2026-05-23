# Fluent Schema Builder

`EventSchemaBuilder` provides a readable, chainable API for defining an event schema entirely in code — no data classes required.

## Basic usage

```csharp
using Hermodr;

var schema = EventSchema.Build("order.placed")
    .WithVersion("1.0")
    .WithContentType("application/json")
    .WithDescription("Raised when a customer places an order")
    .AddProperty("order_id",  "guid",   p => p.Required())
    .AddProperty("amount",    "money",  p => p.Required().WithRange<decimal>(0m, 1_000_000m))
    .AddProperty("currency",  "string", p => p.Required())
    .AddProperty("notes",     "string", p => p.Nullable())
    .Build();
```

`EventSchema.Build(eventType)` returns an `EventSchemaBuilder`.  Call `.Build()` at the end to produce the immutable `EventSchema` object.

## Builder methods

### Schema-level

| Method | Description |
|--------|-------------|
| `.WithVersion(string)` | Sets the schema version (SemVer recommended) |
| `.WithContentType(string)` | Sets the MIME content type of the event data |
| `.WithDescription(string)` | Sets a human-readable description |
| `.AddProperty(name, type, configure?)` | Adds a property with the given name and data type |
| `.AddProperty(name, configure)` | Adds a property using a full configure delegate |

### Property-level (`EventPropertyBuilder`)

| Method | Description |
|--------|-------------|
| `.OfType(string)` | Sets the data type of the property |
| `.Required()` | Marks the property as required |
| `.Nullable()` | Marks the property as nullable |
| `.WithDescription(string)` | Sets a human-readable description |
| `.WithRange<T>(min, max)` | Adds a range constraint |
| `.WithAllowedValues<T>(IReadOnlyList<T>)` | Restricts the property to a set of allowed values |

## Property data types

The schema is type-system agnostic — `DataType` is a plain string.  The following values are used by convention and are recognised by the AsyncAPI exporter:

| String value | Semantic |
|---|---|
| `"string"` | UTF-8 text |
| `"int"` / `"integer"` | 32-bit signed integer |
| `"long"` | 64-bit signed integer |
| `"decimal"` / `"money"` | High-precision decimal number |
| `"float"` / `"double"` | Floating-point number |
| `"bool"` / `"boolean"` | Boolean |
| `"guid"` / `"uuid"` | UUID / GUID |
| `"datetime"` / `"date"` / `"time"` | Date/time value |
| `"object"` | Embedded object |
| `"array"` | Array/list |

You may use any string for custom or domain-specific types.

## Rich property configuration

Use the delegate overload for more control:

```csharp
var schema = EventSchema.Build("user.registered")
    .WithVersion("1.0")
    .AddProperty("email", p => p
        .OfType("string")
        .Required()
        .WithDescription("The email address of the new user"))
    .AddProperty("age", p => p
        .OfType("int")
        .WithRange<int>(18, 120)
        .WithDescription("Age of the user in years"))
    .AddProperty("role", p => p
        .OfType("string")
        .Required()
        .WithAllowedValues<string>(["admin", "user", "guest"]))
    .AddProperty("nickname", p => p
        .OfType("string")
        .Nullable())
    .Build();
```

## Related pages

- [From Annotations](from-annotations.md) — derive a schema from existing data classes
- [Export as JSON](export-json.md)
- [Export as YAML](export-yaml.md)
- [Export as AsyncAPI](export-asyncapi.md)
- [Validation](validation.md)

