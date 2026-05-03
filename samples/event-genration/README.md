# Event Generation + MassTransit (Console)

This sample shows a minimal end-to-end flow where:

1. A `partial` event class is annotated with `[Event]`.
2. The `Deveel.Events.Generators` source generator emits `IEventConvertible`.
3. Assembly-level attributes bake the schema base URI and JSON options into the generated code at compile time.
4. `IEventPublisher.PublishAsync(...)` publishes through `Deveel.Events.Publisher.MassTransit`.
5. A mocked `IPublishEndpoint` captures the outbound `ICloudEventMessage` so you can inspect the payload.

## Project layout

- `EventGeneration.Console/Program.cs` — host setup, assembly attributes, publisher configuration, and publish call
- `EventGeneration.Console/SampleJsonOptions.cs` — static JSON options provider referenced by `[EventJsonSerializationOptions]`
- `EventGeneration.Console/Events/PersonRegistered.cs` — annotated event data class (`partial`)

## Assembly-level generator attributes

Two attributes in `Program.cs` control how the source generator emits `ToCloudEvent()`:

```csharp
// Bakes the full dataschema URI into a compile-time const string.
// The generator computes: "https://schemas.example.com/events/{eventType}/{dataVersion}"
[assembly: EventDataSchemaUri("https://schemas.example.com/events")]

// Emits a direct static call to SampleJsonOptions.GetOptions() instead of
// reading EventGeneratorContext.JsonSerializerOptions at runtime.
[assembly: EventJsonSerializationOptions(typeof(SampleJsonOptions))]
```

`SampleJsonOptions` exposes:

```csharp
public static class SampleJsonOptions
{
    public static JsonSerializerOptions GetOptions() => /* camelCase, no nulls */;
}
```

## Building dependencies

Before running the sample, you need to build the Deveel.Events library dependencies and output them to a `libs` folder. This approach allows you to reference compiled binaries instead of project references.

### Build dependencies

Choose the build script appropriate for your operating system:

**macOS / Linux:**
```bash
cd samples/event-genration
./build-libs.sh
```

**Windows (PowerShell):**
```powershell
cd samples/event-genration
.\build-libs.ps1
```

**Windows (Command Prompt):**
```cmd
cd samples/event-genration
build-libs.bat
```

The build scripts will:
1. Compile all required Deveel.Events library dependencies
2. Copy the compiled binaries to a `libs` folder at the sample root
3. Display the location of the compiled binaries
4. Delegate build/copy logic to shared core scripts in `samples/build-libs-core.sh`, `samples/build-libs-core.ps1`, and `samples/build-libs-core.bat`

### Referencing the binaries

After running the build script, update your project files to reference the binaries from the `libs` folder instead of the project references. See the project .csproj file and update the `<ProjectReference>` items to `<Reference>` items pointing to the `libs` folder.

## Run

```bash
cd samples/event-genration/EventGeneration.Console
dotnet run
```

Expected output includes:

- A line proving generated conversion (`Generated converter produced CloudEvent ...`)
- A line showing the `MassTransit` channel published a `CloudEvent` payload

## Notes

- This sample does not start a real MassTransit bus, so no broker or MassTransit license setup is required.
- The source generator is referenced as an analyzer in `EventGeneration.Console.csproj` via `../libs/Deveel.Events.Generators.dll`.
- Both `[EventDataSchemaUri]` and `[EventJsonSerializationOptions]` are optional and independent — you can use one, both, or neither.
- The `Deveel.Events.Generators` source generator is copied into `libs` and loaded from there as an analyzer.
