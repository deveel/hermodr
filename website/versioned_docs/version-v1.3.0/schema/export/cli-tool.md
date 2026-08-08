# CLI Export Tool

The `Hermodr.AsyncApi.Exporter.Tool` is a `dotnet` global tool that exports
Hermodr event schemas to **AsyncAPI 2.x** or **OpenAPI 3.1** documents without
a running application host. It is designed to run as a CI step so the published
contract always matches the code.

## Install

```bash
dotnet tool install --global Hermodr.AsyncApi.Exporter.Tool
```

## Usage

```text
Usage: hermodr-asyncapi --assembly <path> [--assembly <path> ...]
                       [--output asyncapi|openapi]
                       [--format json|yaml]
                       [--title <title>] [--version <version>]
                       [--out <path>]
                       [--channels-file <path>]
                       [--entry <type>::<method>]
```

### Options

| Option | Description |
|--------|-------------|
| `--assembly <path>` | Assembly to scan for `[Event]`-annotated types (repeatable). |
| `--output <format>` | `asyncapi` (default) or `openapi`. |
| `--format <format>` | `json` (default) or `yaml`. |
| `--title <title>` | `info.title` (default: `Hermodr Events`). |
| `--version <version>` | `info.version` (default: `1.0`). |
| `--out <path>` | Output file path (required). |
| `--channels-file <path>` | JSON file describing channel bindings (see below). |
| `--entry <type>::<method>` | Assembly-qualified type name and a static method (in the form `<type>::<method>`) returning `IReadOnlyList<IEventPublishChannelMetadata>`. |
| `--help`, `-h` | Show help. |

## Channel bindings

The tool has no running host, so transport metadata (RabbitMQ exchange, ASB
queue, webhook URL, ...) must be supplied externally. Two mechanisms are
supported; both may be used together.

### `--channels-file` (JSON)

```json
{
  "channels": [
    {
      "name": "orders",
      "transport": "rabbitmq",
      "properties": { "exchange": "orders.x", "routingKey": "#" }
    },
    {
      "name": "notifications",
      "transport": "webhook",
      "properties": { "url": "https://example.com/hook" }
    }
  ]
}
```

Channels whose `transport` is `"other"` are skipped with a warning (storage /
deferral / recovery layers are not message transports).

### `--entry <type>::<method>`

When the application exposes a static method returning
`IReadOnlyList<IEventPublishChannelMetadata>` (for example a factory that
constructs the channel metadata from configuration), the tool can invoke it
directly without booting a host:

```bash
hermodr-asyncapi \
  --assembly ./bin/MyService.Events.dll \
  --entry MyService.ChannelMetadataFactory, MyService::GetChannels \
  --output asyncapi --format yaml \
  --out ./docs/events.yaml
```

## Examples

### AsyncAPI YAML from a single assembly

```bash
hermodr-asyncapi \
  --assembly ./bin/MyService.Events.dll \
  --output asyncapi --format yaml \
  --title "My Service Events" --version "1.0" \
  --out ./docs/asyncapi.yaml
```

### OpenAPI 3.1 JSON with channel bindings

```bash
hermodr-asyncapi \
  --assembly ./bin/MyService.Events.dll \
  --channels-file ./channels.json \
  --output openapi --format json \
  --out ./docs/openapi.json
```

### CI step (GitHub Actions)

```yaml
- name: Export event contracts
  run: |
    dotnet tool install --global Hermodr.AsyncApi.Exporter.Tool
    hermodr-asyncapi \
      --assembly artifacts/MyService.Events.dll \
      --channels-file ./ci/channels.json \
      --output asyncapi --format yaml \
      --out ./docs/events.yaml
```

## Runtime requirements

The tool single-targets **.NET 10** and rolls forward to later runtime
releases, so installing it only requires the .NET 10 SDK (or a newer one) to
be present. It does not need to match the target framework of the assemblies
being scanned — as long as those assemblies are loadable by the .NET 10
runtime, the export will work.

## AOT and trimming

The tool uses reflection-based assembly scanning and is therefore not suitable
for trimmed / AOT-compiled scenarios. The compile-time source generator
planned for v1.5.0 (roadmap item 21) will provide an AOT-safe metadata source
that replaces runtime discovery.

## Internal architecture

The `dotnet` global tool package (`Hermodr.AsyncApi.Exporter.Tool`) bundles an
internal library, `Hermodr.AsyncApi.Exporter`, that contains the console-free
export orchestration (`ExportService`, `ChannelMetadataFileLoader`,
`EntryChannelResolver`, `ExportOptions`, `ExportResult`). The tool itself is a
thin Spectre.Console.Cli shell that parses arguments, calls
`ExportService.ExportAsync`, and renders the resulting `ExportResult` to the
terminal. The internal library is **not** published as a separate NuGet
package — it ships inside the tool.

## Related pages

- [Export as AsyncAPI](asyncapi.md)
- [Export as OpenAPI 3.1](openapi.md)