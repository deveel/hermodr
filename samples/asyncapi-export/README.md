# asyncapi-export sample

A minimal ASP.NET Core app that exposes the event contract in three forms:

| Endpoint | Output |
|----------|--------|
| `/asyncapi.json` | AsyncAPI 2.x document built from auto-discovered `[Event]` schemas (no channel bindings). |
| `/asyncapi-with-channels.json` | AsyncAPI 2.x document enriched with a declarative RabbitMQ channel binding. |
| `/openapi.json` | OpenAPI 3.1 webhook document. |

## Run

```bash
cd samples/asyncapi-export
dotnet run
```

Then visit:

- http://localhost:5000/asyncapi.json
- http://localhost:5000/asyncapi-with-channels.json
- http://localhost:5000/openapi.json

## CI export (no running host)

The same documents can be produced by the `hermodr-asyncapi` global tool as a CI
step, without booting the app:

```bash
dotnet tool install --global Hermodr.AsyncApi.Exporter.Tool

hermodr-asyncapi \
  --assembly ./samples/asyncapi-export/bin/Debug/net10.0/asyncapi-export.dll \
  --channels-file ./samples/asyncapi-export/channels.json \
  --output asyncapi --format yaml \
  --out ./docs/asyncapi.yaml
```

`channels.json`:

```json
{
  "channels": [
    {
      "name": "order-placed",
      "transport": "rabbitmq",
      "properties": { "exchange": "orders.x", "routingKey": "#" }
    }
  ]
}
```

See [docs/schema/export/cli-tool.md](../../docs/schema/export/cli-tool.md) for
the full CLI reference.