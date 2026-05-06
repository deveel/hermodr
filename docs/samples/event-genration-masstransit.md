# Event generation + MassTransit (console)

This sample demonstrates a compile-time and runtime flow in one place:

- A `partial` event class is annotated with `[Event]`.
- `Deveel.Events.Generators` emits `IEventConvertible` during build.
- Two assembly-level attributes bake the schema URI and JSON options into the generated code at **compile time**.
- `IEventPublisher.PublishAsync(...)` sends through the MassTransit publish channel.

## Location

- `samples/event-genration/EventGeneration.Console`

## Key files

| File | Purpose |
|------|---------|
| `Program.cs` | Host setup, assembly-level generator attributes, publisher configuration |
| `SampleJsonOptions.cs` | Static `GetOptions()` provider referenced by `[EventJsonSerializationOptions]` |
| `Events/PersonRegistered.cs` | Annotated `partial` event class |

## Assembly-level attributes used

```csharp
// Bakes "https://schemas.example.com/events/{eventType}/{dataVersion}" as a const string.
[assembly: EventDataSchemaUri("https://schemas.example.com/events")]

// Generated ToCloudEvent() calls SampleJsonOptions.GetOptions() for serialisation.
[assembly: EventJsonSerializationOptions(typeof(SampleJsonOptions))]
```

## Run

```bash
cd samples/event-genration/EventGeneration.Console
dotnet run
```

You should see output that confirms:

1. the generated `IEventConvertible` path produced a `CloudEvent` with a fully resolved `dataschema`
2. the MassTransit channel published an `ICloudEventMessage`

## Notes

- The sample uses mocked `IPublishEndpoint` / `ISendEndpointProvider`, so no external broker is required.
- Both assembly attributes are optional and independent — remove either to fall back to the runtime `EventGeneratorContext` approach.
- The folder name intentionally follows the requested spelling: `event-genration`.

