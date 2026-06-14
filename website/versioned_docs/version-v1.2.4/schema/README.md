# Event Schema

The `Hermodr.Schema` package adds the ability to formally describe the structure of your events — what properties they carry, their types, and constraints — and to validate `CloudEvent` instances against those descriptions before publishing.

## Installation

```bash
dotnet add package Hermodr.Schema
```

## What's included

| Type | Role |
|------|------|
| `IEventSchema` / `EventSchema` | The schema model |
| `EventSchemaBuilder` | Fluent API for building a schema in code |
| `EventSchemaCreator` | Derives a schema from an annotated class using reflection |
| `IEventSchemaFactory` | DI-friendly factory wrapping `EventSchemaCreator` |
| `IEventSchemaWriter` | Abstraction for schema serialisation (JSON, YAML, AsyncAPI) |
| `EventSchemaJsonWriter` | Serialises a schema to JSON |
| `IEventSchemaValidator` | Validates a `CloudEvent` against a schema |

## Pages in this section

| Page | Description |
|------|-------------|
| [Fluent Builder](fluent-builder.md) | Build a schema programmatically |
| [From Annotations](from-annotations.md) | Derive a schema from `[Event]`-annotated classes |
| [Export as JSON](export-json.md) | Serialise a schema to a JSON document |
| [Export as YAML](export-yaml.md) | Serialise a schema to a YAML document |
| [Export as AsyncAPI](export-asyncapi.md) | Generate complete AsyncAPI 2.x documents |
| [Validation](validation.md) | Validate `CloudEvent` instances against a schema |

## Schema model at a glance

An `IEventSchema` carries:

- **`Type`** — the event type string (e.g. `"order.placed"`)
- **`Version`** — the SemVer version of the schema
- **`ContentType`** — MIME type of the event data
- **`Description`** — human-readable description
- **`Properties`** — a collection of `IEventProperty` entries, each with:
  - `Name`, `DataType`, `Description`
  - Constraints: `Required`, `Nullable`, `Range<T>`, `Enum`

