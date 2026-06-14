# Export as YAML

YAML export serializes event schemas to YAML format, ideal for human-readable documentation, version control, and configuration files.

## When to Use YAML Export

- ✅ Including in documentation sites (GitBook, Docusaurus, etc.)
- ✅ Storing in version control (cleaner diffs than JSON)
- ✅ Reviewing schema changes in pull requests
- ✅ Configuration files for external tools
- ✅ Sharing schemas with non-technical stakeholders

## Installation

```bash
dotnet add package Hermodr.Schema
dotnet add package YamlDotNet  # Required for YAML serialization
```

## Basic Usage

### Export to Stream

```csharp
using Hermodr;
using Hermodr.Schema.Yaml;

var schema = EventSchema.FromDataType<OrderPlacedData>();
var yamlWriter = new EventSchemaYamlWriter();

using var stream = new MemoryStream();
await yamlWriter.WriteToAsync(stream, schema);

stream.Position = 0;
var yaml = await new StreamReader(stream).ReadToEndAsync();
Console.WriteLine(yaml);
```

### Export to File

```csharp
var schema = EventSchema.FromDataType<OrderPlacedData>();
var yamlWriter = new EventSchemaYamlWriter();

using var fileStream = File.Create("order-placed.schema.yaml");
await yamlWriter.WriteToAsync(fileStream, schema);
```

### Export to String

```csharp
var schema = EventSchema.FromDataType<OrderPlacedData>();
var yamlWriter = new EventSchemaYamlWriter();

using var stream = new MemoryStream();
await yamlWriter.WriteToAsync(stream, schema);

stream.Position = 0;
var yaml = await new StreamReader(stream).ReadToEndAsync();
```

## Output Format

### Simple Schema

```csharp
var schema = EventSchema.Build("user.created")
    .WithVersion("1.0")
    .AddProperty("userId", "guid", p => p.Required())
    .AddProperty("email", "string", p => p.Required())
    .Build();
```

Produces:

```yaml
type: user.created
version: '1.0'
contentType: application/json
properties:
  - name: userId
    dataType: guid
    required: true
  - name: email
    dataType: string
    required: true
```

### Schema with Constraints

```csharp
var schema = EventSchema.Build("order.placed")
    .WithVersion("1.0")
    .AddProperty("amount", "money", p => p
        .Required()
        .WithRange<decimal>(0.01m, 1_000_000m))
    .AddProperty("currency", "string", p => p
        .Required()
        .WithAllowedValues<string>(["USD", "EUR", "GBP"]))
    .AddProperty("notes", "string", p => p.Nullable())
    .Build();
```

Produces:

```yaml
type: order.placed
version: '1.0'
contentType: application/json
description: Raised when a customer places an order
properties:
  - name: amount
    dataType: money
    required: true
    constraints:
      min: 0.01
      max: 1000000.0
  - name: currency
    dataType: string
    required: true
    constraints:
      enum:
        - USD
        - EUR
        - GBP
  - name: notes
    dataType: string
    required: false
    nullable: true
```

## YAML vs JSON

### Readability Comparison

**JSON:**
```json
{
  "type": "order.placed",
  "version": "1.0",
  "properties": [
    {
      "name": "amount",
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

**YAML:**
```yaml
type: order.placed
version: '1.0'
properties:
  - name: amount
    dataType: money
    required: true
    constraints:
      min: 0.01
      max: 1000000.0
```

**Benefits of YAML:**
- 40% fewer characters
- No braces or quotes (usually)
- Easier to read and edit manually
- Cleaner Git diffs

### Git Diff Comparison

**JSON diff:**
```diff
-     "properties": [
-       {
-         "name": "amount",
-         "dataType": "money",
-         "required": true
-       }
-     ]
+     "properties": [
+       {
+         "name": "amount",
+         "dataType": "money",
+         "required": true,
+         "constraints": {
+           "min": 0.01
+         }
+       }
+     ]
```

**YAML diff:**
```diff
 properties:
   - name: amount
     dataType: money
     required: true
+    constraints:
+      min: 0.01
```

## Documentation Integration

### Docusaurus

Embed YAML schemas in Docusaurus documentation:

```mdx
## Order Placed Event Schema

```yaml title="order-placed.schema.yaml"
type: order.placed
version: '1.0'
properties:
  - name: amount
    dataType: money
    required: true
```

### GitBook

Use code blocks with titles:

````markdown
## Event Schema

```yaml title="order-placed.schema.yaml"
type: order.placed
version: '1.0'
properties:
  - name: amount
    dataType: money
    required: true
```
````

### GitHub README

Use collapsible sections in Markdown:

````markdown
## Event Schema

<details>
<summary>View YAML schema</summary>

```yaml
type: order.placed
version: '1.0'
properties:
  - name: amount
    dataType: money
    required: true
```

</details>
````

## Related Pages

- [Export Overview](README.md)
- [Export as JSON](json.md)
- [Export as AsyncAPI](asyncapi.md)
