# Export Formats Overview

The `Hermodr.Schema` package can export event schemas in multiple formats, each serving different purposes.

## Available Export Formats

| Format | Use Case | Machine-Readable | Human-Readable | Tooling Support |
|--------|----------|------------------|----------------|-----------------|
| **JSON** | Schema registries, validation libraries, API gateways | ✅ Excellent | ⚠️ Moderate | ✅ Excellent |
| **YAML** | Documentation, version control, configuration files | ✅ Good | ✅ Excellent | ✅ Good |
| **AsyncAPI** | Complete API specifications, documentation generation | ✅ Excellent | ✅ Excellent | ✅ Excellent |

---

## JSON Export

**Best for:** Schema registries, automated validation, tooling integration

### When to Use JSON

- ✅ Integrating with schema registries (e.g., Schema Registry, custom solutions)
- ✅ Using JSON Schema validation libraries
- ✅ API gateways that consume JSON Schema
- ✅ Automated CI/CD validation pipelines
- ✅ Programmatic schema comparison

### Example

```json
{
  "type": "order.placed",
  "version": "1.0",
  "contentType": "application/json",
  "description": "Raised when a customer places an order",
  "properties": [
    {
      "name": "OrderId",
      "dataType": "guid",
      "required": true
    },
    {
      "name": "Amount",
      "dataType": "money",
      "required": true,
      "constraints": {
        "min": 0.01,
        "max": 1000000.0
      }
    }
  ]
}
```

### Learn More

→ [Export as JSON](json.md)

---

## YAML Export

**Best for:** Human-readable documentation, version control diffs, configuration

### When to Use YAML

- ✅ Including in documentation sites (GitBook, Docusaurus, etc.)
- ✅ Storing in version control (cleaner diffs than JSON)
- ✅ Reviewing schema changes in pull requests
- ✅ Configuration files for external tools
- ✅ Sharing schemas with non-technical stakeholders

### Example

```yaml
type: order.placed
version: '1.0'
contentType: application/json
description: Raised when a customer places an order
properties:
  - name: OrderId
    dataType: guid
    required: true
  - name: Amount
    dataType: money
    required: true
    constraints:
      min: 0.01
      max: 1000000.0
```

### Learn More

→ [Export as YAML](yaml.md)

---

## AsyncAPI Export

**Best for:** Complete API specifications, documentation generation, consumer onboarding

### When to Use AsyncAPI

- ✅ Publishing complete API specifications for consumers
- ✅ Generating interactive documentation (e.g., AsyncAPI Studio, ReDoc)
- ✅ Describing channels, bindings, and security requirements
- ✅ Integrating with API governance platforms
- ✅ Creating mock servers and test environments

### What AsyncAPI Provides

AsyncAPI is to async APIs what OpenAPI is to REST APIs. It describes:

- **Event schemas** — Structure of messages
- **Channels** — Where messages are published/subscribed
- **Bindings** — Protocol-specific details (AMQP, Kafka, WebSockets, etc.)
- **Security** — Authentication and authorization requirements
- **Servers** — Broker endpoints and configurations

### Example

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
        $ref: '#/components/messages/OrderPlaced'

components:
  messages:
    OrderPlaced:
      contentType: application/json
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
```

### Learn More

→ [Export as AsyncAPI](asyncapi.md)

---

## Format Selection Guide

### I need to... → Use...

| Scenario | Recommended Format |
|----------|-------------------|
| Validate events programmatically | **JSON** |
| Store in a schema registry | **JSON** |
| Include in documentation | **YAML** or **AsyncAPI** |
| Review changes in Git | **YAML** |
| Publish API specification for consumers | **AsyncAPI** |
| Generate interactive docs | **AsyncAPI** |
| Configure API gateway | **JSON** |
| Share with business stakeholders | **YAML** |

---

## Exporting Multiple Formats

You can export the same schema in multiple formats for different audiences:

```csharp
var schema = EventSchema.FromDataType<OrderPlacedData>();

// JSON for schema registry
var jsonWriter = new EventSchemaJsonWriter();
await jsonWriter.WriteToAsync(jsonStream, schema);

// YAML for documentation
var yamlWriter = new EventSchemaYamlWriter();
await yamlWriter.WriteToAsync(yamlStream, schema);

// AsyncAPI for consumer portal
var asyncApiWriter = new EventSchemaAsyncApiWriter(AsyncApiFormat.Yaml);
await asyncApiWriter.WriteToAsync(asyncApiStream, schema);
```

---

## Related Pages

- [Fluent Builder](../fluent-builder.md)
- [From Annotations](../from-annotations.md)
- [Export as JSON](json.md)
- [Export as YAML](yaml.md)
- [Export as AsyncAPI](asyncapi.md)
