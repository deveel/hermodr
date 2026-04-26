# Schema Validation

`IEventSchemaValidator` validates a `CloudEvent` instance against an `IEventSchema` and reports constraint violations as an asynchronous stream of `ValidationResult` objects.

## Prerequisites

The validator is provided by the `Deveel.Events.Schema` package:

```bash
dotnet add package Deveel.Events.Schema
```

## Interface

```csharp
public interface IEventSchemaValidator
{
    IAsyncEnumerable<ValidationResult> ValidateEventAsync(
        IEventSchema schema,
        CloudEvent @event,
        CancellationToken cancellationToken = default);
}
```

## Registration

`IEventSchemaFactory` and `EventSchemaFactory` are not automatically wired by the publisher builder.  Register them explicitly:

```csharp
using Deveel.Events;
using Microsoft.Extensions.DependencyInjection;

builder.Services.AddSingleton<IEventSchemaFactory, EventSchemaFactory>();
```

> **Note:** `IEventSchemaValidator` defines the validation contract but its default implementation must be supplied by the application, or you can call the static `EventSchema.FromDataType<T>()` helpers and perform validation inline without a direct DI dependency.

## Validating before publishing

```csharp
using CloudNative.CloudEvents;
using Deveel.Events;
using System.ComponentModel.DataAnnotations;

public class OrderService
{
    private readonly IEventSchemaValidator _validator;
    private readonly IEventPublisher _publisher;

    public OrderService(IEventSchemaValidator validator, IEventPublisher publisher)
    {
        _validator = validator;
        _publisher = publisher;
    }

    public async Task PlaceOrderAsync(OrderPlacedData data, CancellationToken ct = default)
    {
        var schema = EventSchema.FromDataType<OrderPlacedData>();

        var @event = new CloudEvent
        {
            Type   = "order.placed",
            Source = new Uri("https://myapp.example.com/orders"),
            Data   = data
        };

        // Collect all validation errors before publishing
        var errors = new List<ValidationResult>();
        await foreach (var result in _validator.ValidateEventAsync(schema, @event, ct))
        {
            errors.Add(result);
        }

        if (errors.Count > 0)
        {
            var messages = string.Join("; ", errors.Select(e => e.ErrorMessage));
            throw new InvalidOperationException($"Event is invalid: {messages}");
        }

        await _publisher.PublishAsync(data, ct);
    }
}
```

## Validated constraints

The validator checks the following constraints declared in the schema against the event's `data` payload:

| Constraint | Checked when |
|------------|--------------|
| `Required` | The property is missing or `null` in the payload |
| `Nullable: false` | The property is explicitly `null` |
| `Range<T>(min, max)` | The value falls outside the range |
| `Enum(values)` | The value is not in the allowed set |

## Streaming results

`ValidateEventAsync` returns `IAsyncEnumerable<ValidationResult>`, so you can process errors lazily or stop early on the first error:

```csharp
// Stop on first violation
await foreach (var result in _validator.ValidateEventAsync(schema, @event))
{
    throw new InvalidOperationException(result.ErrorMessage);
}
```

## Related pages

- [Fluent Builder](fluent-builder.md)
- [From Annotations](from-annotations.md)
- [Event Publisher](../concepts/event-publisher.md)

