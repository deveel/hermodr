# Packages

The framework is split into focused NuGet packages so you only take what you need.

## Core packages

| Package | Description |
|---------|-------------|
| [`Deveel.Events.Annotations`](#deveeleeventsannotations) | Attributes for describing event metadata on your data classes |
| [`Deveel.Events.Publisher`](#deveeleventsublisher) | Core publisher infrastructure (`EventPublisher`, `EventPublisherBuilder`, DI helpers) |

## Channel packages

| Package | Description |
|---------|-------------|
| [`Deveel.Events.Publisher.AzureServiceBus`](#deveeleventsublisherazureservicebus) | Publish events to an Azure Service Bus queue or topic |
| [`Deveel.Events.Publisher.RabbitMq`](#deveeleventsublisherrabbitmq) | Publish events to a RabbitMQ exchange |
| [`Deveel.Events.Publisher.MassTransit`](#deveeleventsublishermasstransit) | Publish events through a MassTransit bus |
| [`Deveel.Events.Publisher.Webhook`](#deveeleventsublisherwebhook) | Deliver events to HTTP webhook endpoints |
| [`Deveel.Events.Publisher.Outbox`](#deveeleventspublisheroutbox) | Persist events to a transactional outbox for later relay |
| [`Deveel.Events.Publisher.Outbox.EntityFramework`](#deveeleventspublisheroutboxentityframework) | Entity Framework Core repository and helpers for the outbox channel |
| [`Deveel.Events.Amqp.Annotations`](#deveeleventsampqannotations) | AMQP-specific attributes (exchange name, routing key) |

## Subscriptions package

| Package | Description |
|---------|-------------|
| [`Deveel.Events.Subscriptions`](#deveeleventssubscriptions) | In-process event subscription registry and dispatcher |

## Schema packages

| Package | Description |
|---------|-------------|
| [`Deveel.Events.Schema`](#deveeleventsschema) | Core schema model, fluent builder, JSON writer, and schema validation |
| [`Deveel.Events.Schema.Yaml`](#deveeleventsschemayaml) | Export an event schema as a YAML document |
| [`Deveel.Events.Schema.AsyncApi`](#deveeleventsschemaasynapi) | Export schemas as an AsyncAPI 2.x document (JSON or YAML) |

## Test package

| Package | Description |
|---------|-------------|
| [`Deveel.Events.TestPublisher`](#deveeleventsestpublisher) | In-memory test channel and helpers for unit tests |

---

## Package Details

### `Deveel.Events.Annotations`

[![NuGet](https://img.shields.io/nuget/v/Deveel.Events.Annotations.svg)](https://www.nuget.org/packages/Deveel.Events.Annotations)
[![GitHub pre-release](https://img.shields.io/badge/nuget-prerelease-yellow?logo=nuget)](https://github.com/deveel/deveel.events/pkgs/nuget/Deveel.Events.Annotations)

Contains the `[Event]` and `[EventProperty]` attributes used to annotate data-transfer classes with event type metadata.  No dependencies except the .NET BCL.

```bash
dotnet add package Deveel.Events.Annotations
```

---

### `Deveel.Events.Publisher`

[![NuGet](https://img.shields.io/nuget/v/Deveel.Events.Publisher.svg)](https://www.nuget.org/packages/Deveel.Events.Publisher)
[![GitHub pre-release](https://img.shields.io/badge/nuget-prerelease-yellow?logo=nuget)](https://github.com/deveel/deveel.events/pkgs/nuget/Deveel.Events.Publisher)

The heart of the framework.  Provides:

- `EventPublisher`
- `IEventPublishChannel` and `IBatchEventPublishChannel`
- `EventPublisherBuilder` for fluent DI registration
- `IEventFactory`, `IEventIdGenerator`, and `IEventSystemTime` extensibility points
- `EventPublisherOptions` for global defaults
- named publisher pipelines resolved through keyed DI

```bash
dotnet add package Deveel.Events.Publisher
```

---

### `Deveel.Events.Publisher.AzureServiceBus`

[![NuGet](https://img.shields.io/nuget/v/Deveel.Events.Publisher.AzureServiceBus.svg)](https://www.nuget.org/packages/Deveel.Events.Publisher.AzureServiceBus)
[![GitHub pre-release](https://img.shields.io/badge/nuget-prerelease-yellow?logo=nuget)](https://github.com/deveel/deveel.events/pkgs/nuget/Deveel.Events.Publisher.AzureServiceBus)

Azure Service Bus channel implementation.  Serialises `CloudEvent` objects as `ServiceBusMessage` instances and sends them to a configured queue or topic.

```bash
dotnet add package Deveel.Events.Publisher.AzureServiceBus
```

---

### `Deveel.Events.Publisher.RabbitMq`

[![NuGet](https://img.shields.io/nuget/v/Deveel.Events.Publisher.RabbitMq.svg)](https://www.nuget.org/packages/Deveel.Events.Publisher.RabbitMq)
[![GitHub pre-release](https://img.shields.io/badge/nuget-prerelease-yellow?logo=nuget)](https://github.com/deveel/deveel.events/pkgs/nuget/Deveel.Events.Publisher.RabbitMq)

RabbitMQ channel implementation using the official RabbitMQ.Client library.  Supports AMQP exchange/routing-key annotations, publisher confirms, and persistent messages.

```bash
dotnet add package Deveel.Events.Publisher.RabbitMq
```

---

### `Deveel.Events.Publisher.MassTransit`

[![NuGet](https://img.shields.io/nuget/v/Deveel.Events.Publisher.MassTransit.svg)](https://www.nuget.org/packages/Deveel.Events.Publisher.MassTransit)
[![GitHub pre-release](https://img.shields.io/badge/nuget-prerelease-yellow?logo=nuget)](https://github.com/deveel/deveel.events/pkgs/nuget/Deveel.Events.Publisher.MassTransit)

Wraps MassTransit's `IPublishEndpoint` / `ISendEndpointProvider` so events can be routed through any MassTransit-supported transport.

```bash
dotnet add package Deveel.Events.Publisher.MassTransit
```

---

### `Deveel.Events.Publisher.Webhook`

[![NuGet](https://img.shields.io/nuget/v/Deveel.Events.Publisher.Webhook.svg)](https://www.nuget.org/packages/Deveel.Events.Publisher.Webhook)
[![GitHub pre-release](https://img.shields.io/badge/nuget-prerelease-yellow?logo=nuget)](https://github.com/deveel/deveel.events/pkgs/nuget/Deveel.Events.Publisher.Webhook)

Delivers events over HTTP to a webhook endpoint.  Features HMAC signing (SHA-256/384/512), exponential-backoff retries, configurable headers, and pluggable serialisers.

```bash
dotnet add package Deveel.Events.Publisher.Webhook
```

---

### `Deveel.Events.Publisher.Outbox`

[![NuGet](https://img.shields.io/nuget/v/Deveel.Events.Publisher.Outbox.svg)](https://www.nuget.org/packages/Deveel.Events.Publisher.Outbox)
[![GitHub pre-release](https://img.shields.io/badge/nuget-prerelease-yellow?logo=nuget)](https://github.com/deveel/deveel.events/pkgs/nuget/Deveel.Events.Publisher.Outbox)

Implements the transactional outbox pattern for event publishing.  It adds the `AddOutbox<TMessage>()` builder entry point, outbox relay services, repository abstractions, and message-factory hooks for persisting events before relaying them to a transport-specific publisher pipeline.

```bash
dotnet add package Deveel.Events.Publisher.Outbox
```

---

### `Deveel.Events.Publisher.Outbox.EntityFramework`

[![NuGet](https://img.shields.io/nuget/v/Deveel.Events.Publisher.Outbox.EntityFramework.svg)](https://www.nuget.org/packages/Deveel.Events.Publisher.Outbox.EntityFramework)
[![GitHub pre-release](https://img.shields.io/badge/nuget-prerelease-yellow?logo=nuget)](https://github.com/deveel/deveel.events/pkgs/nuget/Deveel.Events.Publisher.Outbox.EntityFramework)

Adds Entity Framework Core integration for the outbox channel, including `DbOutboxMessage`, `OutboxDbContext`, and the `WithEntityFramework()` registration helper that wires an `IOutboxMessageRepository<TMessage>` backed by EF Core.

```bash
dotnet add package Deveel.Events.Publisher.Outbox.EntityFramework
```

---

### `Deveel.Events.Amqp.Annotations`

[![NuGet](https://img.shields.io/nuget/v/Deveel.Events.Amqp.Annotations.svg)](https://www.nuget.org/packages/Deveel.Events.Amqp.Annotations)
[![GitHub pre-release](https://img.shields.io/badge/nuget-prerelease-yellow?logo=nuget)](https://github.com/deveel/deveel.events/pkgs/nuget/Deveel.Events.Amqp.Annotations)

Adds `[AmqpExchange]` and `[AmqpRoutingKey]` attributes to let you declare per-event-type AMQP routing metadata directly on your data classes. See [RabbitMQ — AMQP Annotations](publishers/rabbitmq.md#amqp-annotations) for usage details.

```bash
dotnet add package Deveel.Events.Amqp.Annotations
```

---

### `Deveel.Events.Subscriptions`

[![NuGet](https://img.shields.io/nuget/v/Deveel.Events.Subscriptions.svg)](https://www.nuget.org/packages/Deveel.Events.Subscriptions)
[![GitHub pre-release](https://img.shields.io/badge/nuget-prerelease-yellow?logo=nuget)](https://github.com/deveel/deveel.events/pkgs/nuget/Deveel.Events.Subscriptions)

Adds an in-process event subscription registry and dispatcher middleware to the `EventPublisher` pipeline. Includes:

- `IEventSubscription` and `EventSubscription` — the subscription model
- `IEventSubscriptionRegistry` — default in-memory registry
- `IEventSubscriptionResolver` — extensibility point for database- or remote-backed resolvers
- `EventDispatcher` — middleware that queries resolvers and invokes matched handlers
- `EventFilter` / `EventFilterBuilder` — composable `FilterExpression` factory for envelope and data-payload filtering

```bash
dotnet add package Deveel.Events.Subscriptions
```

---

### `Deveel.Events.Schema`

[![NuGet](https://img.shields.io/nuget/v/Deveel.Events.Schema.svg)](https://www.nuget.org/packages/Deveel.Events.Schema)
[![GitHub pre-release](https://img.shields.io/badge/nuget-prerelease-yellow?logo=nuget)](https://github.com/deveel/deveel.events/pkgs/nuget/Deveel.Events.Schema)

Core schema model.  Includes `EventSchema`, `EventSchemaBuilder` (fluent API), `EventSchemaCreator` (reflection-based), `IEventSchemaFactory`, `IEventSchemaWriter`, and `IEventSchemaValidator`.

```bash
dotnet add package Deveel.Events.Schema
```

---

### `Deveel.Events.Schema.Yaml`

[![NuGet](https://img.shields.io/nuget/v/Deveel.Events.Schema.Yaml.svg)](https://www.nuget.org/packages/Deveel.Events.Schema.Yaml)
[![GitHub pre-release](https://img.shields.io/badge/nuget-prerelease-yellow?logo=nuget)](https://github.com/deveel/deveel.events/pkgs/nuget/Deveel.Events.Schema.Yaml)

Adds `EventSchemaYamlWriter`, which serialises an `IEventSchema` to a YAML stream using YamlDotNet.

```bash
dotnet add package Deveel.Events.Schema.Yaml
```

---

### `Deveel.Events.Schema.AsyncApi`

[![NuGet](https://img.shields.io/nuget/v/Deveel.Events.Schema.AsyncApi.svg)](https://www.nuget.org/packages/Deveel.Events.Schema.AsyncApi)
[![GitHub pre-release](https://img.shields.io/badge/nuget-prerelease-yellow?logo=nuget)](https://github.com/deveel/deveel.events/pkgs/nuget/Deveel.Events.Schema.AsyncApi)

Exports single or multiple event schemas as a complete AsyncAPI 2.x document (JSON or YAML).

```bash
dotnet add package Deveel.Events.Schema.AsyncApi
```

---

### `Deveel.Events.TestPublisher`

[![NuGet](https://img.shields.io/nuget/v/Deveel.Events.TestPublisher.svg)](https://www.nuget.org/packages/Deveel.Events.TestPublisher)

An in-memory publish channel for use in unit and integration tests.  Invokes a user-supplied callback whenever an event is published, so you can assert on published events without a real transport.

```bash
dotnet add package Deveel.Events.TestPublisher
```

