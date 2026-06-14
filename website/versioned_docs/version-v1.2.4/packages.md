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
| [`Hermodr.Publisher.Outbox`](#hermodr-publisher-outbox) | Persist events to a transactional outbox for later relay |
| [`Hermodr.Publisher.Outbox.EntityFramework`](#hermodr-publisher-outbox-entityframework) | Entity Framework Core repository and helpers for the outbox channel |
| [`Hermodr.Amqp.Annotations`](#hermodr-amqp-annotations) | AMQP-specific attributes (exchange name, routing key) |

## Subscriptions package

| Package | Description |
|---------|-------------|
| [`Hermodr.Subscriptions`](#hermodr-subscriptions) | In-process event subscription registry and dispatcher |

## Observability package

| Package | Description |
|---------|-------------|
| [`Hermodr.Publisher.OpenTelemetry`](#hermodr-publisher-opentelemetry) | Distributed tracing with W3C trace context propagation via CloudEvents extensions |

## Schema packages

| Package | Description |
|---------|-------------|
| [`Hermodr.Schema`](#hermodr-schema) | Core schema model, fluent builder, JSON writer, and schema validation |
| [`Hermodr.Schema.Yaml`](#hermodr-schema-yaml) | Export an event schema as a YAML document |
| [`Hermodr.Schema.AsyncApi`](#hermodr-schema-asyncapi) | Export schemas as an AsyncAPI 2.x document (JSON or YAML) |

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

### `Hermodr.Publisher.Outbox`

[![NuGet](https://img.shields.io/nuget/v/Hermodr.Publisher.Outbox.svg)](https://www.nuget.org/packages/Hermodr.Publisher.Outbox)
[![GitHub pre-release](https://img.shields.io/badge/nuget-prerelease-yellow?logo=nuget)](https://github.com/deveel/hermodr/pkgs/nuget/Hermodr.Publisher.Outbox)

Implements the transactional outbox pattern for event publishing.  It adds the `AddOutbox<TMessage>()` builder entry point, outbox relay services, repository abstractions, and message-factory hooks for persisting events before relaying them to a transport-specific publisher pipeline.

```bash
dotnet add package Hermodr.Publisher.Outbox
```

---

### `Hermodr.Publisher.Outbox.EntityFramework`

[![NuGet](https://img.shields.io/nuget/v/Hermodr.Publisher.Outbox.EntityFramework.svg)](https://www.nuget.org/packages/Hermodr.Publisher.Outbox.EntityFramework)
[![GitHub pre-release](https://img.shields.io/badge/nuget-prerelease-yellow?logo=nuget)](https://github.com/deveel/hermodr/pkgs/nuget/Hermodr.Publisher.Outbox.EntityFramework)

Adds Entity Framework Core integration for the outbox channel, including `DbOutboxMessage`, `OutboxDbContext`, and the `WithEntityFramework()` registration helper that wires an `IOutboxMessageRepository<TMessage>` backed by EF Core.

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

