# Export as AsyncAPI

AsyncAPI export generates complete AsyncAPI 2.x specification documents that describe your event-driven APIs, including schemas, channels, bindings, and security requirements.

## When to Use AsyncAPI Export

- ✅ Publishing complete API specifications for consumers
- ✅ Generating interactive documentation (AsyncAPI Studio, ReDoc)
- ✅ Describing channels, bindings, and protocol-specific details
- ✅ Integrating with API governance platforms
- ✅ Creating mock servers and test environments

## What Is AsyncAPI?

AsyncAPI is to async APIs what OpenAPI is to REST APIs. It describes:

| Component | Description |
|-----------|-------------|
| **Servers** | Broker endpoints (RabbitMQ, Kafka, Azure Service Bus, etc.) |
| **Channels** | Topics, queues, or exchanges where messages are published |
| **Operations** | Publish and subscribe operations |
| **Messages** | Event schemas and metadata |
| **Security** | Authentication and authorization requirements |
| **Bindings** | Protocol-specific configuration |

## Installation

```bash
dotnet add package Hermodr.Schema
dotnet add package Hermodr.Schema.AsyncApi
```

## Basic Usage

### Minimal AsyncAPI Document

```csharp
using Hermodr;
using Hermodr.Schema.AsyncApi;

var schema = EventSchema.FromDataType<OrderPlacedData>();

var asyncApiWriter = new EventSchemaAsyncApiWriter(AsyncApiFormat.Yaml);

var document = new AsyncApiDocument
{
    Info = new AsyncApiInfo
    {
        Title = "Order Service API",
        Version = "1.0.0",
        Description = "Order management event API"
    },
    Events = new[] { schema }
};

using var stream = new MemoryStream();
await asyncApiWriter.WriteToAsync(stream, document);

stream.Position = 0;
var yaml = await new StreamReader(stream).ReadToEndAsync();
Console.WriteLine(yaml);
```

### Output

```yaml
asyncapi: 2.6.0
info:
  title: Order Service API
  version: 1.0.0
  description: Order management event API

channels:
  orders/placed:
    publish:
      operationId: publishOrderPlaced
      message:
        payload:
          type: object
          properties:
            OrderId:
              type: string
              format: uuid
            Amount:
              type: number
              minimum: 0.01
              maximum: 1000000.0
            Currency:
              type: string
```

## Advanced Configuration

### With Server Configuration

```csharp
var document = new AsyncApiDocument
{
    Info = new AsyncApiInfo
    {
        Title = "Order Service API",
        Version = "1.0.0"
    },
    Servers = new[]
    {
        new AsyncApiServer
        {
            Url = "amqp://rabbitmq.example.com",
            Protocol = "amqp",
            Description = "Production RabbitMQ broker"
        }
    },
    Events = new[] { schema }
};
```

### With Security Requirements

```csharp
var document = new AsyncApiDocument
{
    Info = new AsyncApiInfo
    {
        Title = "Order Service API",
        Version = "1.0.0"
    },
    SecuritySchemes = new[]
    {
        new AsyncApiSecurityScheme
        {
            Type = "userPassword",
            Description = "Basic authentication"
        }
    },
    Events = new[] { schema }
};
```

### With Protocol Bindings

```csharp
var document = new AsyncApiDocument
{
    Info = new AsyncApiInfo
    {
        Title = "Order Service API",
        Version = "1.0.0"
    },
    Events = new[]
    {
        new AsyncApiEvent
        {
            Schema = schema,
            Channel = "orders.events",
            Bindings = new AsyncApiBindings
            {
                Amqp = new AmqpChannelBinding
                {
                    Exchange = new AmqpExchange
                    {
                        Name = "orders",
                        Type = "topic",
                        Durable = true,
                        AutoDelete = false
                    },
                    RoutingKey = "order.placed"
                }
            }
        }
    }
};
```

## Output Formats

### YAML Format (Recommended)

```csharp
var writer = new EventSchemaAsyncApiWriter(AsyncApiFormat.Yaml);
```

**Best for:**
- Documentation
- Version control
- Manual editing
- Code reviews

### JSON Format

```csharp
var writer = new EventSchemaAsyncApiWriter(AsyncApiFormat.Json);
```

**Best for:**
- Programmatic processing
- API gateway configuration
- Schema registries

## AsyncAPI Versions

The AsyncAPI writer supports:

| Version | Features |
|---------|----------|
| **2.6.0** | Latest stable, recommended |
| **2.5.0** | Previous stable |
| **2.4.0** | Legacy support |

```csharp
var writer = new EventSchemaAsyncApiWriter(AsyncApiFormat.Yaml, AsyncApiVersion.V2_6_0);
```

## Documentation Generation

### AsyncAPI Studio

1. Export your AsyncAPI document
2. Paste into [AsyncAPI Studio](https://studio.asyncapi.com/)
3. View interactive documentation
4. Share with consumers

### ReDoc

```bash
# Install @asyncapi/cli
npm install -g @asyncapi/cli

# Generate HTML documentation
asyncapi generate html order-service-api.yaml --output docs/index.html
```

### Static Site Generation

```bash
# Using @asyncapi/generator
asyncapi generate fromTemplate order-service-api.yaml @asyncapi/html-template --output docs
```

## Complete Example

```csharp
using Hermodr;
using Hermodr.Schema.AsyncApi;

// Define schemas
var orderPlacedSchema = EventSchema.FromDataType<OrderPlacedData>();
var orderConfirmedSchema = EventSchema.FromDataType<OrderConfirmedData>();

// Build AsyncAPI document
var document = new AsyncApiDocument
{
    Info = new AsyncApiInfo
    {
        Title = "Order Service API",
        Version = "1.0.0",
        Description = "Event-driven API for order management",
        Contact = new AsyncApiContact
        {
            Name = "API Team",
            Email = "api@example.com"
        }
    },
    Servers = new[]
    {
        new AsyncApiServer
        {
            Url = "amqp://rabbitmq.example.com",
            Protocol = "amqp",
            Description = "Production RabbitMQ"
        },
        new AsyncApiServer
        {
            Url = "amqp://rabbitmq-staging.example.com",
            Protocol = "amqp",
            Description = "Staging RabbitMQ"
        }
    },
    Events = new[]
    {
        new AsyncApiEvent
        {
            Schema = orderPlacedSchema,
            Channel = "orders.placed",
            Operation = new AsyncApiOperation
            {
                OperationId = "publishOrderPlaced",
                Description = "Publish when an order is placed"
            }
        },
        new AsyncApiEvent
        {
            Schema = orderConfirmedSchema,
            Channel = "orders.confirmed",
            Operation = new AsyncApiOperation
            {
                OperationId = "publishOrderConfirmed",
                Description = "Publish when an order is confirmed"
            }
        }
    }
};

// Export
var writer = new EventSchemaAsyncApiWriter(AsyncApiFormat.Yaml);
using var stream = new MemoryStream();
await writer.WriteToAsync(stream, document);

stream.Position = 0;
var yaml = await new StreamReader(stream).ReadToEndAsync();
File.WriteAllText("asyncapi.yaml", yaml);
```

## Related Pages

- [Export Overview](README.md)
- [Export as JSON](json.md)
- [Export as YAML](yaml.md)
