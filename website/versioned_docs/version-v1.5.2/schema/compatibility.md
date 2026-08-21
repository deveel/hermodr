# Schema Compatibility

The `Hermodr.Schema` package includes a compatibility checker that compares two versions of an event schema and reports whether the change is backward compatible, forward compatible, both, or breaking in both directions.

## Compatibility levels

| Level | Meaning |
|-------|---------|
| `Full` | Existing readers and writers of both versions can interoperate. |
| `Backward` | Readers of the newer schema can consume data written with the older schema. |
| `Forward` | Readers of the older schema can consume data written with the newer schema. |
| `None` | The change is breaking in both directions. |

## Using the checker

`IEventSchemaCompatibilityChecker` is registered automatically when you call `AddEventSchemaServices()`. Inject it or resolve it from the service provider:

```csharp
public class SchemaEvolutionValidator
{
    private readonly IEventSchemaCompatibilityChecker _checker;

    public SchemaEvolutionValidator(IEventSchemaCompatibilityChecker checker)
    {
        _checker = checker;
    }

    public bool IsSafeToDeploy(IEventSchema v1, IEventSchema v2)
    {
        var result = _checker.Check(v1, v2);
        return result.Level is SchemaCompatibilityLevel.Backward
                      or SchemaCompatibilityLevel.Full;
    }
}
```

## What is detected

The checker walks the property tree recursively and reports:

| Change | Backward breaking | Forward breaking |
|--------|-------------------|------------------|
| Required property added | Yes | No |
| Optional property added | No | No |
| Property removed | No | Yes |
| Property type changed | Yes | Yes |
| Property became required | Yes | No |
| Property became optional | No | Yes |
| Range constraint narrowed | Yes | No |
| Range constraint widened | No | Yes |
| Enum values removed | Yes | No |
| Enum values added | No | Yes |

Property renames are detected as a remove + add, because the checker does not attempt to match properties by anything other than name.

## Interpreting the result

```csharp
var result = _checker.Check(schemaV1, schemaV2);

Console.WriteLine($"Level: {result.Level}");

foreach (var change in result.Changes)
{
    Console.WriteLine($"{change.Path}: {change.Message}");
    Console.WriteLine($"  Backward breaking: {change.IsBackwardBreaking}");
    Console.WriteLine($"  Forward breaking:  {change.IsForwardBreaking}");
}
```

## CI quality gate

Because the checker has no runtime dependencies beyond the schema model, it is easy to use in a unit test or CI step that loads two schema files and fails the build when a deployment would introduce a backward-incompatible change.

```csharp
[Fact]
public void OrderPlaced_V2_Must_Be_Backward_Compatible()
{
    var v1 = new EventSchema("order.placed", "1.0.0", "application/json");
    v1.Properties.Add(new EventProperty("orderId", "string", "1.0.0"));

    var v2 = new EventSchema("order.placed", "2.0.0", "application/json");
    v2.Properties.Add(new EventProperty("orderId", "string", "2.0.0"));
    v2.Properties.Add(new EventProperty("customerEmail", "string", "2.0.0") { IsNullable = true });

    var result = new EventSchemaCompatibilityChecker().Check(v1, v2);

    Assert.True(
        result.Level is SchemaCompatibilityLevel.Backward or SchemaCompatibilityLevel.Full,
        string.Join(Environment.NewLine, result.Changes.Select(c => c.Message)));
}
```

> The compatibility checker only inspects the schema shape and constraints. It does not validate default values, custom converter behaviour, or application-level semantics.
