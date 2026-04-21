![GitHub License](https://img.shields.io/github/license/deveel/deveel.events)
![GitHub Release](https://img.shields.io/github/v/release/deveel/deveel.events) ![GitHub Actions Workflow Status](https://img.shields.io/github/actions/workflow/status/deveel/deveel.events/cicd.yml?logo=github)
![Codecov](https://img.shields.io/codecov/c/github/deveel/deveel.events?logo=codecov)


# Deveel Events

This project provides a simple and lightweight framework for publishing events to subscribers, using common channels and topics.

The ambition of this little framework is to implement a set of common patterns and practices that can be used to implement a simple and efficient event-driven architecture in a .NET application, using common approaches to create events and publish them.

It is not in the scope of this project to provide a full-featured event storage system, nor a complex pub/sub application: if you need such a system, you should consider using a message broker or a message queue system (such as RabbitMQ, Kafka, or Azure Service Bus) to implement a more complex and scalable event-driven architecture).

## Motivation

Often when developing applications, it is necessary to implement a mechanism to notify other parts of the system about changes or events that occur in the application: several times the implementor ends up rewriting boilerplate code to manage the events and notifications.

At the present time, there are several ways to implement such a mechanism, such as using a message broker, a message queue, or a pub/sub system, but every organization should implement its own way to manage events and notifications.

With this small effort, we aim to provide a simple and lightweight framework that can be used to implement a common way to publish events in a .NET application.

## CloudEvents Standard

The framework is designed to be compliant with the [CloudEvents](https://cloudevents.io/) standard, making use of the `CloudEvent` class to represent the reference model for the event.

This choice is made to ensure the maximum compatibility with other systems and services that are compliant with the standard, and to provide a simple and efficient way to serialize and deserialize events.

## Installation

The framework is available as a NuGet package, and can be installed using the `dotnet` cli.

For example, to install the package that provides the publisher to the Azure Service Bus, you can run the following command:

```bash
dotnet add package Deveel.Events.Publisher.AzureServiceBus
```

Alternatively, you can use the NuGet package manager in Visual Studio to search and install the package.

### Framework Packages

Packages provided by the framework are:

| Package | Description | NuGet Package | Pre-Release<br/>(GitHub Packages) |
|---------|-------------|---------------|-------------------------------|
| `Deveel.Events.Annotations` | A set of attributes used to describe the metadata of an event | [![NuGet](https://img.shields.io/nuget/v/Deveel.Events.Annotations.svg)](https://www.nuget.org/packages/Deveel.Events.Annotations) | [![GitHub](https://img.shields.io/badge/nuget-prerelease-yellow?logo=nuget)](https://github.com/deveel/deveel.events/pkgs/nuget/Deveel.Events.Annotations) |
| `Deveel.Events.Publisher` | The core framework for publishing events | [![NuGet](https://img.shields.io/nuget/v/Deveel.Events.Publisher.svg)](https://www.nuget.org/packages/Deveel.Events.Publisher) | [![GitHub](https://img.shields.io/badge/nuget-prerelease-yellow?logo=nuget)](https://github.com/deveel/deveel.events/pkgs/nuget/Deveel.Events.Publisher) |
| `Deveel.Events.Publisher.AzureServiceBus` | An implementation of the publisher using Azure Service Bus | [![NuGet](https://img.shields.io/nuget/v/Deveel.Events.Publisher.AzureServiceBus.svg)](https://www.nuget.org/packages/Deveel.Events.Publisher.AzureServiceBus) | [![GitHub](https://img.shields.io/badge/nuget-prerelease-yellow?logo=nuget)](https://github.com/deveel/deveel.events/pkgs/nuget/Deveel.Events.Publisher.AzureServiceBus) |
| `Deveel.Events.Amqp.Annotations` | A set of attributes used to describe the metadata of an event published in AMQP queues | [![NuGet](https://img.shields.io/nuget/v/Deveel.Events.Amqp.Annotations.svg)](https://www.nuget.org/packages/Deveel.Events.Amqp.Annotations) | [![GitHub](https://img.shields.io/badge/nuget-prerelease-yellow?logo=nuget)](https://github.com/deveel/deveel.events/pkgs/nuget/Deveel.Events.Amqp.Annotations) |
| `Deveel.Events.Publisher.RabbitMq` | An implementation of the publisher using RabbitMQ as a channel | [![NuGet](https://img.shields.io/nuget/v/Deveel.Events.Publisher.RabbitMq.svg)](https://www.nuget.org/packages/Deveel.Events.Publisher.RabbitMq) | [![GitHub](https://img.shields.io/badge/nuget-prerelease-yellow?logo=nuget)](https://github.com/deveel/deveel.events/pkgs/nuget/Deveel.Events.Publisher.RabbitMq) |
| `Deveel.Events.Schema` | Core schema model, fluent builder, JSON writer, and schema validation | [![NuGet](https://img.shields.io/nuget/v/Deveel.Events.Schema.svg)](https://www.nuget.org/packages/Deveel.Events.Schema) | [![GitHub](https://img.shields.io/badge/nuget-prerelease-yellow?logo=nuget)](https://github.com/deveel/deveel.events/pkgs/nuget/Deveel.Events.Schema) |
| `Deveel.Events.Schema.Yaml` | Exports an event schema as a YAML document | [![NuGet](https://img.shields.io/nuget/v/Deveel.Events.Schema.Yaml.svg)](https://www.nuget.org/packages/Deveel.Events.Schema.Yaml) | [![GitHub](https://img.shields.io/badge/nuget-prerelease-yellow?logo=nuget)](https://github.com/deveel/deveel.events/pkgs/nuget/Deveel.Events.Schema.Yaml) |
| `Deveel.Events.Schema.AsyncApi` | Exports one or more event schemas as an AsyncAPI 2.x document (JSON or YAML) | [![NuGet](https://img.shields.io/nuget/v/Deveel.Events.Schema.AsyncApi.svg)](https://www.nuget.org/packages/Deveel.Events.Schema.AsyncApi) | [![GitHub](https://img.shields.io/badge/nuget-prerelease-yellow?logo=nuget)](https://github.com/deveel/deveel.events/pkgs/nuget/Deveel.Events.Schema.AsyncApi) |

## Usage

The basic usage of the framework is to create an event, publish it to a channel, and subscribe to the channel to receive the event.

The following example shows how to create an event, publish it to a channel, and subscribe to the channel to receive the event.

To enable this capability, you must first register the publisher in the service collection of your application:

```csharp
using Deveel.Events;

var builder = WebApplication.CreateBuilder(args);

// ...

builder.Services.AddEventPublisher();
```

Then, you can create an event and publish it to a channel:

```csharp
using Deveel.Events;

public class MyService {
    private readonly IEventPublisher publisher;

    public MyService(IEventPublisher publisher) {
        this.publisher = publisher;
    }

    public async Task PublishEventAsync() {
        var @event = new CloudEvent("com.example.myevent", new Uri("http://example.com/events/123"), "Hello, World!") {
            ContentType = "text/plain",
            DataSchema = new Uri("http://example.com/schema"),
            Source = new Uri("http://example.com"),
            Data = "Hello, World!"
        };

        await publisher.PublishEventAsync(@event);
    }
}
```

Note that the above example will publish the event to all the channels that are registered in the publisher.

### Publishing from Event Data

If you have a class that represents the data of an event, you can use the `EventAttribute` to decorate such a class to describe the metadata of the event containing it.

For example, consider the following class:

```csharp
using Deveel.Events.Annotations;

[Event("com.example.myevent", "1.0")]
public class MyEventData {
    [Required]
    public string Message { get; set; }
}
```

You can then publish an event using the data class:

```csharp
using Deveel.Events;

public class MyService {
    private readonly IEventPublisher publisher;

    public MyService(IEventPublisher publisher) {
        this.publisher = publisher;
    }
    
    public async Task PublishEventAsync() {
        var data = new MyEventData {
            Message = "Hello, World!"
        };
        
        await publisher.PublishAsync(data);
    }
}
```

## Event Schema

The `Deveel.Events.Schema` package provides a way to describe and validate the structure of events through schemas, ensuring that events conform to an expected contract.

### Installation

```bash
dotnet add package Deveel.Events.Schema
```

### Creating a Schema with the Fluent Builder

The `EventSchema.Build()` factory method returns an `EventSchemaBuilder` that allows you to describe every aspect of an event schema in a fluent, readable style:

```csharp
using Deveel.Events;

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

You can also describe a property with a configure delegate for richer control:

```csharp
var schema = EventSchema.Build("user.registered")
    .WithVersion("1.0")
    .AddProperty("email", p => p
        .OfType("string")
        .Required()
        .WithDescription("The email address of the new user"))
    .AddProperty("age", p => p
        .OfType("int")
        .WithRange<int>(18, 120))
    .AddProperty("nickname", p => p
        .OfType("string")
        .Nullable())
    .Build();
```

### Generating a Schema from an Annotated Class

If you already have a class decorated with `[Event]` and standard data-annotation attributes, you can derive the schema automatically without writing it by hand:

```csharp
using System.ComponentModel.DataAnnotations;
using Deveel.Events.Annotations;

[Event("order.placed", "1.0")]
public class OrderPlacedData {
    [Required]
    public Guid OrderId { get; set; }

    [Required]
    [Range(0.0, 1_000_000.0)]
    public decimal Amount { get; set; }

    [Required]
    public string Currency { get; set; } = default!;

    public string? Notes { get; set; }
}
```

Use the static helper to create the schema:

```csharp
// Via the static convenience method
var schema = EventSchema.FromDataType<OrderPlacedData>();

// Or via the injectable factory (preferred in DI scenarios)
using Deveel.Events;

public class MyService {
    private readonly IEventSchemaFactory schemaFactory;

    public MyService(IEventSchemaFactory schemaFactory) {
        this.schemaFactory = schemaFactory;
    }

    public EventSchema GetSchema() => schemaFactory.CreateFromType<OrderPlacedData>();
}
```

### Exporting the Schema as JSON

A schema can be serialised to a JSON stream with `EventSchemaJsonWriter`:

```csharp
using Deveel.Events;
using System.Text.Json;

var schema = EventSchema.FromDataType<OrderPlacedData>();

var writer = new EventSchemaJsonWriter(new JsonWriterOptions { Indented = true });

await using var stream = File.OpenWrite("order-placed-schema.json");
await writer.WriteToAsync(stream, schema);
```

The output will look similar to:

```json
{
  "type": "order.placed",
  "version": "1.0",
  "contentType": "object",
  "properties": {
    "OrderId": { "dataType": "guid", "required": true },
    "Amount":  { "dataType": "money", "required": true, "min": 0, "max": 1000000 },
    "Currency":{ "dataType": "string", "required": true },
    "Notes":   { "dataType": "string", "nullable": true }
  }
}
```

### Exporting the Schema as YAML

Install the package:

```bash
dotnet add package Deveel.Events.Schema.Yaml
```

Use `EventSchemaYamlWriter` (which implements `IEventSchemaWriter`) to serialise a schema to a YAML stream:

```csharp
using Deveel.Events;

var schema = EventSchema.FromDataType<OrderPlacedData>();

var writer = new EventSchemaYamlWriter();   // uses camelCase by default

await using var stream = File.OpenWrite("order-placed-schema.yaml");
await writer.WriteToAsync(stream, schema);
```

The output will look similar to:

```yaml
type: order.placed
version: 1.0
contentType: object
properties:
  orderId:
    dataType: guid
    required: true
  amount:
    dataType: money
    required: true
    min: 0
    max: 1000000
  currency:
    dataType: string
    required: true
  notes:
    dataType: string
    nullable: true
```

You can supply a custom `YamlDotNet.Serialization.ISerializer` to the constructor to control naming conventions, anchors, and other serialization options.

### Exporting the Schema as AsyncAPI

Install the package:

```bash
dotnet add package Deveel.Events.Schema.AsyncApi
```

#### Single schema → standalone AsyncAPI document

`EventSchemaAsyncApiWriter` wraps a single `IEventSchema` in a fully valid AsyncAPI 2.x document. It exposes the schema under `components/schemas`, declares a corresponding message under `components/messages`, and wires a subscribe channel:

```csharp
using Deveel.Events;

var schema = EventSchema.FromDataType<OrderPlacedData>();

// Output as JSON (default)
var writer = new EventSchemaAsyncApiWriter(
    format: AsyncApiFormat.Json,
    title: "Order Events",
    documentVersion: "1.0");

await using var stream = File.OpenWrite("order-placed-asyncapi.json");
await writer.WriteToAsync(stream, schema);
```

To produce YAML instead, pass `AsyncApiFormat.Yaml`:

```csharp
var writer = new EventSchemaAsyncApiWriter(AsyncApiFormat.Yaml);

await using var stream = File.OpenWrite("order-placed-asyncapi.yaml");
await writer.WriteToAsync(stream, schema);
```

#### Multiple schemas → combined AsyncAPI document

`EventSchemasAsyncApiWriter` merges several schemas into a single AsyncAPI document — useful for generating a service-wide contract file:

```csharp
using Deveel.Events;

IEnumerable<IEventSchema> schemas = new[] {
    EventSchema.FromDataType<OrderPlacedData>(),
    EventSchema.FromDataType<OrderCancelledData>(),
    EventSchema.FromDataType<UserRegisteredData>()
};

var writer = new EventSchemasAsyncApiWriter(
    title: "My Service Events",
    version: "2.0",
    format: AsyncApiFormat.Yaml);

await using var stream = File.OpenWrite("events-asyncapi.yaml");
await writer.WriteToAsync(stream, schemas);
```

#### Using the extension methods directly

The `EventSchemaAsyncApiExtensions` class exposes lower-level helpers when you need finer control:

```csharp
using Deveel.Events;
using NJsonSchema;
using Saunter.AsyncApiSchema.v2;

var schema = EventSchema.FromDataType<OrderPlacedData>();

// Convert to NJsonSchema
JsonSchema jsonSchema = schema.ToJsonSchema();

// Convert to an AsyncAPI Message
Message message = schema.ToAsyncApiMessage();

// Build a standalone AsyncApiDocument
AsyncApiDocument document = schema.ToAsyncApiDocument(title: "Order Events", version: "1.0");

// Or add to an existing document
var existingDoc = new AsyncApiDocument { /* ... */ };
existingDoc.AddSchema(schema);
```

### Validating an Event Against a Schema

The `IEventSchemaValidator` service validates a `CloudEvent` instance against an `IEventSchema` and returns an asynchronous stream of `ValidationResult` objects — one per violated constraint.

```csharp
using CloudNative.CloudEvents;
using Deveel.Events;
using System.ComponentModel.DataAnnotations;

public class OrderService {
    private readonly IEventSchemaValidator validator;
    private readonly IEventPublisher publisher;

    public OrderService(IEventSchemaValidator validator, IEventPublisher publisher) {
        this.validator = validator;
        this.publisher = publisher;
    }

    public async Task PlaceOrderAsync(OrderPlacedData data) {
        var schema = EventSchema.FromDataType<OrderPlacedData>();

        var @event = new CloudEvent {
            Type    = "order.placed",
            Source  = new Uri("https://myapp.example.com/orders"),
            Data    = data
        };

        // Collect all validation errors before publishing
        var errors = new List<ValidationResult>();
        await foreach (var result in validator.ValidateEventAsync(schema, @event)) {
            errors.Add(result);
        }

        if (errors.Count > 0) {
            var messages = string.Join(", ", errors.Select(e => e.ErrorMessage));
            throw new InvalidOperationException($"Event is invalid: {messages}");
        }

        await publisher.PublishAsync(data);
    }
}
```

> **Tip:** Register the validator (and the schema factory) via the DI container by including the `Deveel.Events.Schema` services in your `IServiceCollection`.

## Future Work

The framework is still in its early stages, and there are several areas that need to be improved and extended.

Some of the areas that we plan to work on in the future are:

- Supporting custom event serializers and deserializers
- Implementing more publishers for different messaging systems (eg. RabbitMQ, Kafka, etc.)
- Supporting the deserialization of events from channels, to make consistent the published events are consumed
- Allow the selection of the channel to publish an event among the registered ones (eg. with named channels)

You can monitor the list of [open issues](https://github.com/deveel/deveel.events/issues) to see what we are working on and what we plan to do in the future.

## Contributing

We welcome contributions to the project, and we encourage you to submit issues and pull requests to help us improve the framework.

If you want to contribute to the project, please read the [Contributing Guidelines](CONTRIBUTING.md) file to understand how to contribute to the project.

## License

The project is released under the [MIT License](LICENSE), and it is free to use and distribute for any purpose.

See the [License](LICENSE) file for more information.

## Authors

The project is developed and maintained by the [Deveel](https://deveel.com) team, and it is released as an open-source project to the community.