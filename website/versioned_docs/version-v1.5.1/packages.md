> **Renamed:** This project was formerly called **Deveel Events**. As of **23 May 2026**, the NuGet packages have been renamed from `Deveel.Events.*` to `Hermodr.*`. The old packages remain published and will be deprecated.

# Packages

The framework is split into focused NuGet packages so you only take what you need.

## Core packages

| Package | Description |
|---------|-------------|
| [`Hermodr.Annotations`](#hermodr-annotations) | Attributes for describing event metadata on your data classes |
| [`Hermodr.Publisher`](#hermodr-publisher) | Core publisher infrastructure (`EventPublisher`, `EventPublisherBuilder`, DI helpers) |

## Channel packages

| Package | Description |
|---------|-------------|
| [`Hermodr.Publisher.AzureServiceBus`](#hermodr-publisher-azureservicebus) | Publish events to an Azure Service Bus queue or topic |
| [`Hermodr.Publisher.RabbitMq`](#hermodr-publisher-rabbitmq) | Publish events to a RabbitMQ exchange |
| [`Hermodr.Publisher.MassTransit`](#hermodr-publisher-masstransit) | Publish events through a MassTransit bus |
| [`Hermodr.Publisher.Webhook`](#hermodr-publisher-webhook) | Deliver events to HTTP webhook endpoints |
| [`Hermodr.Publisher.Dapr`](#hermodr-publisher-dapr) | Publish events through the Dapr pub/sub building block |
| [`Hermodr.Publisher.Outbox`](#hermodr-publisher-outbox) | Persist events to a transactional outbox for later relay |
| [`Hermodr.Publisher.Outbox.EntityFramework`](#hermodr-publisher-outbox-entityframework) | Entity Framework Core store and helpers for the outbox channel |
| [`Hermodr.Publisher.DeliveryLog`](#hermodr-publisher-deliverylog) | Core delivery log middleware and storage abstractions |
| [`Hermodr.Publisher.DeliveryLog.InMemory`](#hermodr-publisher-deliverylog-inmemory) | In-memory storage backend for delivery log records |
| [`Hermodr.Publisher.DeliveryLog.NDJson`](#hermodr-publisher-deliverylog-ndjson) | NDJson rolling-file backend for delivery log records |
| [`Hermodr.Publisher.DeliveryLog.EntityFramework`](#hermodr-publisher-deliverylog-entityframework) | Entity Framework Core persistence for delivery log records |
| [`Hermodr.Amqp.Annotations`](#hermodr-amqp-annotations) | AMQP-specific attributes (exchange name, routing key) |

## Subscriptions package

| Package | Description |
|---------|-------------|
| [`Hermodr.Subscriptions`](#hermodr-subscriptions) | In-process event subscription registry and dispatcher |

## Observability package

| Package | Description |
|---------|-------------|
| [`Hermodr.Publisher.OpenTelemetry`](#hermodr-publisher-opentelemetry) | Distributed tracing with W3C trace context propagation via CloudEvents extensions |

## Audit Trail packages

| Package | Description |
|---------|-------------|
| [`Hermodr.AuditTrail`](#hermodr-audittrail) | Core contracts for an append-only audit trail (`IAuditTrailWriter`, `IAuditTrailReader<T>`, `AuditTrailBuilder`, `AuditTrailStreamQuery`) |
| [`Hermodr.Publisher.AuditTrail`](#hermodr-publisher-audittrail) | Publisher integration: `AuditTrailPublishChannel` and `AddAuditTrail()` extension method |
| [`Hermodr.AuditTrail.InMemory`](#hermodr-audittrail-inmemory) | In-memory storage backend for testing and development |
| [`Hermodr.AuditTrail.EntityFramework`](#hermodr-audittrail-entityframework) | Entity Framework Core storage backend (SQL Server, PostgreSQL, SQLite) |
| [`Hermodr.AuditTrail.NDJson`](#hermodr-audittrail-ndjson) | NDJson file storage backend with pluggable filesystem (Azure Blob, S3, local disk) |

## Schema packages

| Package | Description |
|---------|-------------|
| [`Hermodr.Schema`](#hermodr-schema) | Core schema model, fluent builder, JSON writer, and schema validation |
| [`Hermodr.Schema.Yaml`](#hermodr-schema-yaml) | Export an event schema as a YAML document |
| [`Hermodr.Schema.AsyncApi`](#hermodr-schema-asyncapi) | Export schemas as an AsyncAPI 2.x document (JSON or YAML) |
| [`Hermodr.Publisher.EventSchema`](#hermodr-publisher-eventschema) | Automatic schema validation middleware for the publish pipeline |

## Test package

| Package | Description |
|---------|-------------|
| [`Hermodr.TestPublisher`](#hermodr-testpublisher) | In-memory test channel and helpers for unit tests |

---

## Package Details

### `Hermodr.Annotations`

[![NuGet](https://img.shields.io/nuget/v/Hermodr.Annotations.svg)](https://www.nuget.org/packages/Hermodr.Annotations)
[![GitHub pre-release](https://img.shields.io/badge/nuget-prerelease-yellow?logo=nuget)](https://github.com/deveel/hermodr/pkgs/nuget/Hermodr.Annotations)

Contains the `[Event]` and `[EventProperty]` attributes used to annotate data-transfer classes with event type metadata.  No dependencies except the .NET BCL.

```bash
dotnet add package Hermodr.Annotations
```

---

### `Hermodr.Publisher`

[![NuGet](https://img.shields.io/nuget/v/Hermodr.Publisher.svg)](https://www.nuget.org/packages/Hermodr.Publisher)
[![GitHub pre-release](https://img.shields.io/badge/nuget-prerelease-yellow?logo=nuget)](https://github.com/deveel/hermodr/pkgs/nuget/Hermodr.Publisher)

The heart of the framework.  Provides:

- `EventPublisher`
- `IEventPublishChannel` and `IBatchEventPublishChannel`
- `EventPublisherBuilder` for fluent DI registration
- `IEventFactory`, `IEventIdGenerator`, and `IEventSystemTime` extensibility points
- `EventPublisherOptions` for global defaults
- named publisher pipelines resolved through keyed DI

```bash
dotnet add package Hermodr.Publisher
```

---

### `Hermodr.Publisher.AzureServiceBus`

[![NuGet](https://img.shields.io/nuget/v/Hermodr.Publisher.AzureServiceBus.svg)](https://www.nuget.org/packages/Hermodr.Publisher.AzureServiceBus)
[![GitHub pre-release](https://img.shields.io/badge/nuget-prerelease-yellow?logo=nuget)](https://github.com/deveel/hermodr/pkgs/nuget/Hermodr.Publisher.AzureServiceBus)

Azure Service Bus channel implementation.  Serialises `CloudEvent` objects as `ServiceBusMessage` instances and sends them to a configured queue or topic.

```bash
dotnet add package Hermodr.Publisher.AzureServiceBus
```

---

### `Hermodr.Publisher.RabbitMq`

[![NuGet](https://img.shields.io/nuget/v/Hermodr.Publisher.RabbitMq.svg)](https://www.nuget.org/packages/Hermodr.Publisher.RabbitMq)
[![GitHub pre-release](https://img.shields.io/badge/nuget-prerelease-yellow?logo=nuget)](https://github.com/deveel/hermodr/pkgs/nuget/Hermodr.Publisher.RabbitMq)

RabbitMQ channel implementation using the official RabbitMQ.Client library.  Supports AMQP exchange/routing-key annotations, publisher confirms, and persistent messages.

```bash
dotnet add package Hermodr.Publisher.RabbitMq
```

---

### `Hermodr.Publisher.MassTransit`

[![NuGet](https://img.shields.io/nuget/v/Hermodr.Publisher.MassTransit.svg)](https://www.nuget.org/packages/Hermodr.Publisher.MassTransit)
[![GitHub pre-release](https://img.shields.io/badge/nuget-prerelease-yellow?logo=nuget)](https://github.com/deveel/hermodr/pkgs/nuget/Hermodr.Publisher.MassTransit)

Wraps MassTransit's `IPublishEndpoint` / `ISendEndpointProvider` so events can be routed through any MassTransit-supported transport.

```bash
dotnet add package Hermodr.Publisher.MassTransit
```

---

### `Hermodr.Publisher.Webhook`

[![NuGet](https://img.shields.io/nuget/v/Hermodr.Publisher.Webhook.svg)](https://www.nuget.org/packages/Hermodr.Publisher.Webhook)
[![GitHub pre-release](https://img.shields.io/badge/nuget-prerelease-yellow?logo=nuget)](https://github.com/deveel/hermodr/pkgs/nuget/Hermodr.Publisher.Webhook)

Delivers events over HTTP to a webhook endpoint.  Features HMAC signing (SHA-256/384/512), exponential-backoff retries, configurable headers, and pluggable serialisers.

```bash
dotnet add package Hermodr.Publisher.Webhook
```

---

### `Hermodr.Publisher.Dapr`

[![NuGet](https://img.shields.io/nuget/v/Hermodr.Publisher.Dapr.svg)](https://www.nuget.org/packages/Hermodr.Publisher.Dapr)
[![GitHub pre-release](https://img.shields.io/badge/nuget-prerelease-yellow?logo=nuget)](https://github.com/deveel/hermodr/pkgs/nuget/Hermodr.Publisher.Dapr)

Publishes events through the Dapr pub/sub building block, abstracting the underlying broker (Kafka, RabbitMQ, Azure Service Bus, NATS, ...) behind Dapr component configuration. Supports typed channels, CloudEvent attribute metadata mapping, OpenTelemetry, and the reliability pipeline.

```bash
dotnet add package Hermodr.Publisher.Dapr
```

---

### `Hermodr.Publisher.Outbox`

[![NuGet](https://img.shields.io/nuget/v/Hermodr.Publisher.Outbox.svg)](https://www.nuget.org/packages/Hermodr.Publisher.Outbox)
[![GitHub pre-release](https://img.shields.io/badge/nuget-prerelease-yellow?logo=nuget)](https://github.com/deveel/hermodr/pkgs/nuget/Hermodr.Publisher.Outbox)

Implements the transactional outbox pattern for event publishing.  It adds the `AddOutbox<TMessage>()` builder entry point, outbox relay services, the `IOutboxMessageStore<TMessage>` abstraction, and message-factory hooks for persisting events before relaying them to a transport-specific publisher pipeline.

```bash
dotnet add package Hermodr.Publisher.Outbox
```

---

### `Hermodr.Publisher.Outbox.EntityFramework`

[![NuGet](https://img.shields.io/nuget/v/Hermodr.Publisher.Outbox.EntityFramework.svg)](https://www.nuget.org/packages/Hermodr.Publisher.Outbox.EntityFramework)
[![GitHub pre-release](https://img.shields.io/badge/nuget-prerelease-yellow?logo=nuget)](https://github.com/deveel/hermodr/pkgs/nuget/Hermodr.Publisher.Outbox.EntityFramework)

Adds Entity Framework Core integration for the outbox channel, including `DbOutboxMessage`, `OutboxDbContext`, and the `WithEntityFramework()` registration helper that wires an `IOutboxMessageStore<TMessage>` backed by EF Core.

```bash
dotnet add package Hermodr.Publisher.Outbox.EntityFramework
```

---

### `Hermodr.Amqp.Annotations`

[![NuGet](https://img.shields.io/nuget/v/Hermodr.Amqp.Annotations.svg)](https://www.nuget.org/packages/Hermodr.Amqp.Annotations)
[![GitHub pre-release](https://img.shields.io/badge/nuget-prerelease-yellow?logo=nuget)](https://github.com/deveel/hermodr/pkgs/nuget/Hermodr.Amqp.Annotations)

Adds `[AmqpExchange]` and `[AmqpRoutingKey]` attributes to let you declare per-event-type AMQP routing metadata directly on your data classes. See [RabbitMQ — AMQP Annotations](publishers/rabbitmq.md#amqp-annotations) for usage details.

```bash
dotnet add package Hermodr.Amqp.Annotations
```

---

### `Hermodr.Subscriptions`

[![NuGet](https://img.shields.io/nuget/v/Hermodr.Subscriptions.svg)](https://www.nuget.org/packages/Hermodr.Subscriptions)
[![GitHub pre-release](https://img.shields.io/badge/nuget-prerelease-yellow?logo=nuget)](https://github.com/deveel/hermodr/pkgs/nuget/Hermodr.Subscriptions)

Adds an in-process event subscription registry and dispatcher middleware to the `EventPublisher` pipeline. Includes:

- `IEventSubscription` and `EventSubscription` — the subscription model
- `IEventSubscriptionRegistry` — default in-memory registry
- `IEventSubscriptionResolver` — extensibility point for database- or remote-backed resolvers
- `EventDispatcher` — middleware that queries resolvers and invokes matched handlers
- `EventFilter` / `EventFilterBuilder` — composable `FilterExpression` factory for envelope and data-payload filtering

```bash
dotnet add package Hermodr.Subscriptions
```

---

### `Hermodr.Publisher.OpenTelemetry`

[![NuGet](https://img.shields.io/nuget/v/Hermodr.Publisher.OpenTelemetry.svg)](https://www.nuget.org/packages/Hermodr.Publisher.OpenTelemetry)

Adds OpenTelemetry instrumentation to the event publishing pipeline. Creates producer and consumer `Activity` spans, injects W3C `traceparent`/`tracestate` as CloudEvents extension attributes on publish, and extracts them on the subscription side to enable end-to-end distributed tracing across service boundaries.

```bash
dotnet add package Hermodr.Publisher.OpenTelemetry
```

See [OpenTelemetry Instrumentation](publishers/opentelemetry.md) for the full guide.

---

### `Hermodr.Publisher.DeliveryLog`

[![NuGet](https://img.shields.io/nuget/v/Hermodr.Publisher.DeliveryLog.svg)](https://www.nuget.org/packages/Hermodr.Publisher.DeliveryLog)
[![GitHub pre-release](https://img.shields.io/badge/nuget-prerelease-yellow?logo=nuget)](https://github.com/deveel/hermodr/pkgs/nuget/Hermodr.Publisher.DeliveryLog)

Core delivery log package. Provides the `DeliveryLogMiddleware`, `IEventPublishDeliveryLog` abstraction, and `DeliveryLogBuilder` for configuring pluggable storage backends.

```bash
dotnet add package Hermodr.Publisher.DeliveryLog
```

---

### `Hermodr.Publisher.DeliveryLog.InMemory`

[![NuGet](https://img.shields.io/nuget/v/Hermodr.Publisher.DeliveryLog.InMemory.svg)](https://www.nuget.org/packages/Hermodr.Publisher.DeliveryLog.InMemory)
[![GitHub pre-release](https://img.shields.io/badge/nuget-prerelease-yellow?logo=nuget)](https://github.com/deveel/hermodr/pkgs/nuget/Hermodr.Publisher.DeliveryLog.InMemory)

In-memory storage backend for the delivery log. All records are held in a thread-safe, volatile collection. Suitable for tests and local development, but records are lost on process restart.

```bash
dotnet add package Hermodr.Publisher.DeliveryLog.InMemory
```

---

### `Hermodr.Publisher.DeliveryLog.NDJson`

[![NuGet](https://img.shields.io/nuget/v/Hermodr.Publisher.DeliveryLog.NDJson.svg)](https://www.nuget.org/packages/Hermodr.Publisher.DeliveryLog.NDJson)
[![GitHub pre-release](https://img.shields.io/badge/nuget-prerelease-yellow?logo=nuget)](https://github.com/deveel/hermodr/pkgs/nuget/Hermodr.Publisher.DeliveryLog.NDJson)

Newline-delimited JSON (NDJSON) rolling-file backend for the delivery log. Appends each record as a JSON line to sequentially-named files with automatic file rolling by size or time interval and retention policies.

```bash
dotnet add package Hermodr.Publisher.DeliveryLog.NDJson
```

---

### `Hermodr.Publisher.DeliveryLog.EntityFramework`

[![NuGet](https://img.shields.io/nuget/v/Hermodr.Publisher.DeliveryLog.EntityFramework.svg)](https://www.nuget.org/packages/Hermodr.Publisher.DeliveryLog.EntityFramework)
[![GitHub pre-release](https://img.shields.io/badge/nuget-prerelease-yellow?logo=nuget)](https://github.com/deveel/hermodr/pkgs/nuget/Hermodr.Publisher.DeliveryLog.EntityFramework)

Entity Framework Core storage backend for the delivery log. Stores records in a relational database using `Kista.EntityFramework`.

```bash
dotnet add package Hermodr.Publisher.DeliveryLog.EntityFramework
```

---

### `Hermodr.Schema`

[![NuGet](https://img.shields.io/nuget/v/Hermodr.Schema.svg)](https://www.nuget.org/packages/Hermodr.Schema)
[![GitHub pre-release](https://img.shields.io/badge/nuget-prerelease-yellow?logo=nuget)](https://github.com/deveel/hermodr/pkgs/nuget/Hermodr.Schema)

Core schema model.  Includes `EventSchema`, `EventSchemaBuilder` (fluent API), `EventSchemaCreator` (reflection-based), `IEventSchemaFactory`, `IEventSchemaWriter`, and `IEventSchemaValidator`.

```bash
dotnet add package Hermodr.Schema
```

---

### `Hermodr.Schema.Yaml`

[![NuGet](https://img.shields.io/nuget/v/Hermodr.Schema.Yaml.svg)](https://www.nuget.org/packages/Hermodr.Schema.Yaml)
[![GitHub pre-release](https://img.shields.io/badge/nuget-prerelease-yellow?logo=nuget)](https://github.com/deveel/hermodr/pkgs/nuget/Hermodr.Schema.Yaml)

Adds `EventSchemaYamlWriter`, which serialises an `IEventSchema` to a YAML stream using YamlDotNet.

```bash
dotnet add package Hermodr.Schema.Yaml
```

---

### `Hermodr.Schema.AsyncApi`

[![NuGet](https://img.shields.io/nuget/v/Hermodr.Schema.AsyncApi.svg)](https://www.nuget.org/packages/Hermodr.Schema.AsyncApi)
[![GitHub pre-release](https://img.shields.io/badge/nuget-prerelease-yellow?logo=nuget)](https://github.com/deveel/hermodr/pkgs/nuget/Hermodr.Schema.AsyncApi)

Exports single or multiple event schemas as a complete AsyncAPI 2.x document (JSON or YAML).

```bash
dotnet add package Hermodr.Schema.AsyncApi
```

---

### `Hermodr.TestPublisher`

[![NuGet](https://img.shields.io/nuget/v/Hermodr.TestPublisher.svg)](https://www.nuget.org/packages/Hermodr.TestPublisher)

An in-memory publish channel for use in unit and integration tests.  Invokes a user-supplied callback whenever an event is published, so you can assert on published events without a real transport.

```bash
dotnet add package Hermodr.TestPublisher
```

---

### `Hermodr.AuditTrail`

[![NuGet](https://img.shields.io/nuget/v/Hermodr.AuditTrail.svg)](https://www.nuget.org/packages/Hermodr.AuditTrail)
[![GitHub pre-release](https://img.shields.io/badge/nuget-prerelease-yellow.svg?logo=nuget)](https://github.com/deveel/hermodr/pkgs/nuget/Hermodr.AuditTrail)

Core contracts for an append-only audit trail. Provides `IAuditTrailWriter`, `IAuditTrailReader<TEntry>`, `AuditTrailEntry`, `AuditTrailStreamQuery`, and the `AuditTrailBuilder` used by the publisher integration.

```bash
dotnet add package Hermodr.AuditTrail
```

---

### `Hermodr.Publisher.AuditTrail`

[![NuGet](https://img.shields.io/nuget/v/Hermodr.Publisher.AuditTrail.svg)](https://www.nuget.org/packages/Hermodr.Publisher.AuditTrail)
[![GitHub pre-release](https://img.shields.io/badge/nuget-prerelease-yellow.svg?logo=nuget)](https://github.com/deveel/hermodr/pkgs/nuget/Hermodr.Publisher.AuditTrail)

Publisher integration: `AuditTrailPublishChannel` and the `AddAuditTrail()` extension method. Use it together with one of the audit trail storage backends (`Hermodr.AuditTrail.InMemory`, `Hermodr.AuditTrail.EntityFramework`, or `Hermodr.AuditTrail.NDJson`).

```bash
dotnet add package Hermodr.Publisher.AuditTrail
```

---

### `Hermodr.AuditTrail.InMemory`

[![NuGet](https://img.shields.io/nuget/v/Hermodr.AuditTrail.InMemory.svg)](https://www.nuget.org/packages/Hermodr.AuditTrail.InMemory)
[![GitHub pre-release](https://img.shields.io/badge/nuget-prerelease-yellow.svg?logo=nuget)](https://github.com/deveel/hermodr/pkgs/nuget/Hermodr.AuditTrail.InMemory)

In-memory storage backend for the audit trail. Suitable for testing and development.

```bash
dotnet add package Hermodr.AuditTrail.InMemory
```

---

### `Hermodr.AuditTrail.EntityFramework`

[![NuGet](https://img.shields.io/nuget/v/Hermodr.AuditTrail.EntityFramework.svg)](https://www.nuget.org/packages/Hermodr.AuditTrail.EntityFramework)
[![GitHub pre-release](https://img.shields.io/badge/nuget-prerelease-yellow.svg?logo=nuget)](https://github.com/deveel/hermodr/pkgs/nuget/Hermodr.AuditTrail.EntityFramework)

Entity Framework Core storage backend for the audit trail. Supports SQL Server, PostgreSQL, and SQLite.

```bash
dotnet add package Hermodr.AuditTrail.EntityFramework
```

---

### `Hermodr.AuditTrail.NDJson`

[![NuGet](https://img.shields.io/nuget/v/Hermodr.AuditTrail.NDJson.svg)](https://www.nuget.org/packages/Hermodr.AuditTrail.NDJson)
[![GitHub pre-release](https://img.shields.io/badge/nuget-prerelease-yellow.svg?logo=nuget)](https://github.com/deveel/hermodr/pkgs/nuget/Hermodr.AuditTrail.NDJson)

Newline-delimited JSON (NDJSON) file storage backend for the audit trail, with automatic file rolling by size or time interval and retention policies. All I/O is performed through the `IFileSystem` abstraction from [`System.IO.Abstractions`](https://www.nuget.org/packages/System.IO.Abstractions), so the local disk can be replaced with Azure Blob Storage, Amazon S3, or any other compatible filesystem by registering a different `IFileSystem` implementation. Reads are **asynchronous and non-blocking**: files are opened with `FileShare.ReadWrite` and streamed line-by-line, so a concurrent writer is never blocked and the full file content is never loaded into memory.

```csharp
builder.Services.AddEventPublisher(o => o.Source = new Uri("https://example.com"))
    .AddAuditTrail(audit => audit.UseNDJson(o =>
    {
        o.DirectoryPath = "./audit-trail";
        o.MaxFileSizeBytes = 10 * 1024 * 1024;
        o.RollInterval = TimeSpan.FromHours(1);
        o.MaxFileCount = 100;
        o.ReadBufferSize = 65_536;  // optional: tune for large files
    }));
```

```bash
dotnet add package Hermodr.AuditTrail.NDJson
```

See the [Audit Trail with NDJson Files](samples/audit-trail-ndjson.md) sample for a full walkthrough.

---

### `Hermodr.Publisher.EventSchema`

[![NuGet](https://img.shields.io/nuget/v/Hermodr.Publisher.EventSchema.svg)](https://www.nuget.org/packages/Hermodr.Publisher.EventSchema)

Adds a middleware component that automatically validates every event's payload against its registered schema before dispatch. Supports format-agnostic validation (JSON, XML), version-aware schema lookup, and configurable behaviors for missing schemas and validation failures.

```bash
dotnet add package Hermodr.Publisher.EventSchema
```

See [Schema Validation at Publish Time](schema/validation-at-publish.md) for the full guide.

