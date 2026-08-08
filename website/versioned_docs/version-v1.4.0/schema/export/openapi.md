# Export as OpenAPI 3.1 Webhooks

The `Hermodr.Schema.OpenApi` package produces **OpenAPI 3.1** documents whose
`webhooks` section exposes one webhook entry per event type. This makes the
event contract accessible to REST-centric tooling (Swagger UI, Stoplight,
Postman, code generators such as Kiota) that consumes OpenAPI natively.

## When to use OpenAPI vs AsyncAPI

| Need | Choose |
|------|--------|
| REST-centric tooling, Swagger UI, Kiota codegen | **OpenAPI 3.1** |
| Native async messaging spec, AMQP/Kafka bindings, broker docs | **AsyncAPI 2.x** |
| Both audiences | Export both from the same schemas |

OpenAPI 3.1 has no concept of channel bindings or servers for webhooks; the
transport identifier carried by `IEventPublishChannelMetadata` is preserved on
each webhook operation via the `x-hermodr-transport` extension so the contract
remains lossless.

## Programmatic export

### Single schema

```csharp
using Hermodr;

var schema = EventSchema.FromDataType<OrderPlacedData>();

var writer = new EventSchemaOpenApiWriter(OpenApiFormat.Json);
await using var fs = File.Create("openapi.json");
await writer.WriteToAsync(fs, schema);
```

### Multiple schemas with channel metadata

```csharp
var schemas = new EventSchemaDiscovery()
        .DiscoverSchemas(typeof(OrderPlacedData).Assembly);

var channels = publisher.GetChannelMetadata(); // from Hermodr.Publisher

var doc = new OpenApiWebhookDocumentBuilder()
    .WithTitle("Order Service Events")
    .WithVersion("1.0")
    .WithSchemas(schemas)
    .WithChannels(channels)
    .Build();

using var fs = File.Create("openapi.json");
OpenApiDocumentWriter.Serialize(fs, doc, OpenApiFormat.Json);
```

### Auto-discovery from assemblies

`EventSchemaDiscovery` scans one or more assemblies for classes annotated with
`[Event]` that carry a `DataVersion`, and returns a schema for each. Types that
use a `DataSchema` URI instead of a version are skipped, mirroring
`EventSchemaRegistry.ScanAssembly`.

```csharp
var schemas = new EventSchemaDiscovery()
    .DiscoverSchemas(Assembly.Load("MyService.Events"));
```

> **Note:** discovery uses reflection and is not suitable for trimmed / AOT
> deployments. The compile-time source generator planned for v1.5.0 (roadmap
> item 21) will provide an AOT-safe alternative.

## Excluding non-transport channels

Channels whose `IEventPublishChannelMetadata.Transport` equals
`EventTransports.Other` (Audit Trail, Outbox, Dead-Letter, ...) are
**excluded** from the exported document. They are storage / deferral / recovery
layers, not message transports, and do not belong in the public contract.

## Lossless transport passthrough

For transports that have no native OpenAPI representation (e.g. `kafka`,
`nats`, `azureservicebus`), the builder emits the transport identifier and the
channel `Properties` bag as operation extensions:

```yaml
webhooks:
  order-placed:
    post:
      operationId: publish_order-placed
      x-hermodr-transport: kafka
      x-hermodr-properties:
        topic: orders.v1
      requestBody: { ... }
```

## Related pages

- [Export as AsyncAPI](asyncapi.md)
- [CLI Export Tool](cli-tool.md)
- [Fluent Builder](../fluent-builder.md)
- [From Annotations](../from-annotations.md)