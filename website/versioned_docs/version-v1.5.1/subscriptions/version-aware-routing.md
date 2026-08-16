# Version-Aware Routing

The `Hermodr.Subscriptions` package can route events based on the schema version they carry. This lets subscribers pin themselves to a specific version, or to an inclusive range, without being affected by newer schema evolutions.

## Where the version is read from

The subscription evaluator reads the schema version from, in order:

1. The `dataversion` CloudEvents extension attribute.
2. The last path segment of the `dataschema` URI.

If neither is present, version-based filters do not match.

## Filtering by exact version

```csharp
// Match only order.placed events at schema version 1.0
builder.Subscribe(
    EventFilter.All(
        EventFilter.ByType("order.placed"),
        EventFilter.BySchemaVersion("1.0")),
    HandleV1Order);
```

## Filtering by version range

```csharp
// Match order.placed events from version 1.0 through 2.5 inclusive
builder.Subscribe(
    EventFilter.All(
        EventFilter.ByType("order.placed"),
        EventFilter.BySchemaVersionRange(minVersion: "1.0", maxVersion: "2.5")),
    HandleOrder);

// Match version 2.0 and later
builder.Subscribe(
    EventFilter.All(
        EventFilter.ByType("order.placed"),
        EventFilter.BySchemaVersionRange(minVersion: "2.0", maxVersion: null)),
    HandleV2PlusOrder);

// Match up to version 1.9 inclusive
builder.Subscribe(
    EventFilter.All(
        EventFilter.ByType("order.placed"),
        EventFilter.BySchemaVersionRange(minVersion: null, maxVersion: "1.9")),
    HandleLegacyOrder);
```

## Fluent builder

The same filters are available through `EventFilterBuilder`:

```csharp
FilterExpression filter = EventFilter.New()
    .ByType("order.placed")
    .BySchemaVersionRange("2.0", "3.0")
    .Build();

builder.Subscribe(filter, HandleOrder);
```

## Version comparison

Versions are parsed as `System.Version` instances before comparison. This avoids the common lexical pitfall where `"1.10"` sorts before `"1.2"`.

## Combining with other filters

Version filters compose with any other subscription filter:

```csharp
FilterExpression filter = EventFilter.New()
    .ByTypePattern("com.example.order.*")
    .BySchemaVersionRange("2.0", "3.0")
    .WithField("payment.amount", FilterExpressionType.GreaterThan, 500.0)
    .Build();
```

## Use cases

- **Blue/green consumers**: deploy a new consumer that only handles the new schema version while the old consumer continues to handle legacy versions.
- **Replay boundaries**: replay events from an audit trail or event store and route each version to the handler that understands it.
- **Canary subscriptions**: test a new handler against a narrow version range before fully cutting over.
