[![GitHub License](https://img.shields.io/github/license/deveel/deveel.events)](https://github.com/deveel/deveel.events/blob/main/LICENSE)
[![GitHub Release](https://img.shields.io/github/v/release/deveel/deveel.events)](https://github.com/deveel/deveel.events/releases) [![GitHub Actions Workflow Status](https://img.shields.io/github/actions/workflow/status/deveel/deveel.events/cicd.yml?logo=github)](https://github.com/deveel/deveel.events/actions/workflows/cicd.yml)
[![Codecov](https://img.shields.io/codecov/c/github/deveel/deveel.events?logo=codecov)](https://codecov.io/gh/deveel/deveel.events)
[![.NET](https://img.shields.io/badge/-8%20%7C%209%20%7C%2010-512BD4?logo=dotnet)](https://dotnet.microsoft.com/download)

# Deveel Events

**Deveel Events** is a lightweight, extensible framework for publishing domain events in .NET applications, built on top of the [CloudEvents](https://cloudevents.io/) standard.

The ambition of this framework is to implement a set of common patterns and practices for a simple and efficient event-driven architecture in .NET, without reinventing the wheel every time a team needs to broadcast domain events.

It is not in the scope of this project to provide a full-featured event storage system or a complex pub/sub platform. If you need those capabilities, consider pairing this library with a dedicated message broker — Deveel Events already ships channel adapters for the most popular ones.

## Domain Events and DDD

[Domain-Driven Design (DDD)](https://martinfowler.com/bliki/DomainDrivenDesign.html) treats **domain events** as first-class citizens of the model — facts about something that _happened_ inside the domain, named in the ubiquitous language of domain experts (`OrderPlaced`, `InvoiceIssued`, `UserRegistered`).

Key properties of domain events:

- **Immutable facts** — they describe what happened, not what should happen.
- **Decoupled producers and consumers** — the producing bounded context does not need to know who is listening.
- **Cross-context integration** — events are the preferred way to share information across bounded contexts without tight coupling.
- **Temporal decoupling** — consumers can process events asynchronously, at their own pace.

Deveel Events implements the **publishing side** of this pattern: a transport-agnostic layer that broadcasts domain events from a bounded context to any number of downstream consumers, without prescribing how you model aggregates or build read models.

## Event Schemas as Async API Contracts

Publishing an event is only half the story. Consumers need to know the **shape** of each event — its properties, types, and constraints — to deserialise it correctly and build reliable integrations. Without a formal contract, field renames silently break consumers and integration knowledge lives only in tribal memory.

**Event schemas** fill the same role for asynchronous messaging that OpenAPI/Swagger fills for REST APIs. The `Deveel.Events.Schema` package can derive a schema automatically from annotated data classes and export it as:

- **JSON Schema** — for schema-registry integration and tooling.
- **YAML** — for human-readable, version-controlled contract documents.
- **AsyncAPI 2.x** — a complete, machine-readable async API specification. AsyncAPI tooling can generate documentation sites, client SDKs, and mock servers from it.

Treat schemas as public API contracts: version them, prefer additive changes, and communicate breaking changes in advance.

## Motivation

Applications frequently need to notify other parts of the system about domain events. Teams end up rewriting the same boilerplate — serialising payloads, constructing envelopes, wiring up transport clients — over and over again.

Deveel Events provides a single, consistent way to publish events across any transport, so teams can focus on domain logic instead of infrastructure plumbing.

## CloudEvents Standard

All events are modelled as [`CloudEvent`](https://cloudevents.io/) objects, ensuring maximum interoperability with cloud platforms and services that implement the CNCF CloudEvents specification.

## Requirements

All packages in this solution multi-target the following runtimes:

| Runtime | Version |
|---------|---------|
| .NET | 8, 9, 10 |

> **Note:** `Deveel.Events.Schema.AsyncApi` also requires the **ASP.NET Core** shared framework (`Microsoft.AspNetCore.App`), since it integrates with the Saunter AsyncAPI middleware.

Every package requires the **Microsoft Dependency Injection** infrastructure (`Microsoft.Extensions.DependencyInjection`). Below are the additional per-package dependencies automatically pulled in as transitive NuGet references:

| Package | Key Dependencies |
|---------|-----------------|
| `Deveel.Events.Annotations` | *(none — pure attribute library)* |
| `Deveel.Events.Publisher` | `CloudNative.CloudEvents` · `Microsoft.Extensions.Options` · `Microsoft.Extensions.Logging.Abstractions` |
| `Deveel.Events.Publisher.AzureServiceBus` | `Azure.Messaging.ServiceBus` ≥ 7.20 |
| `Deveel.Events.Publisher.RabbitMq` | `RabbitMQ.Client` ≥ 7.2 · `Deveel.Events.Amqp.Annotations` |
| `Deveel.Events.Publisher.MassTransit` | `MassTransit` ≥ 9.1 |
| `Deveel.Events.Publisher.Webhook` | `Microsoft.Extensions.Http` · `Polly` ≥ 7.2 |
| `Deveel.Events.Subscriptions` | `Deveel.Events.Publisher` · `Microsoft.Extensions.Logging.Abstractions` |
| `Deveel.Events.Schema` | `CloudNative.CloudEvents` |
| `Deveel.Events.Schema.Yaml` | `YamlDotNet` ≥ 16.3 |
| `Deveel.Events.Schema.AsyncApi` | `Saunter` ≥ 0.13 · `YamlDotNet` ≥ 16.3 · ASP.NET Core shared framework |

## Packages

### Publishing

| Package | Description | NuGet (Stable) | Pre-release | Downloads |
|---------|-------------|---------------|-------------|-----------|
| `Deveel.Events.Annotations` | Attributes for describing event metadata on data classes | [![NuGet](https://img.shields.io/nuget/v/Deveel.Events.Annotations.svg)](https://www.nuget.org/packages/Deveel.Events.Annotations) | [![GitHub pre-release](https://img.shields.io/nuget/vpre/Deveel.Events.Annotations.svg?label=pre-release)](https://github.com/orgs/deveel/packages/nuget/package/Deveel.Events.Annotations) | [![NuGet Downloads](https://img.shields.io/nuget/dt/Deveel.Events.Annotations.svg)](https://www.nuget.org/packages/Deveel.Events.Annotations) |
| `Deveel.Events.Publisher` | Core publisher infrastructure (`IEventPublisher`, DI helpers) | [![NuGet](https://img.shields.io/nuget/v/Deveel.Events.Publisher.svg)](https://www.nuget.org/packages/Deveel.Events.Publisher) | [![GitHub pre-release](https://img.shields.io/nuget/vpre/Deveel.Events.Publisher.svg?label=pre-release)](https://github.com/orgs/deveel/packages/nuget/package/Deveel.Events.Publisher) | [![NuGet Downloads](https://img.shields.io/nuget/dt/Deveel.Events.Publisher.svg)](https://www.nuget.org/packages/Deveel.Events.Publisher) |
| `Deveel.Events.Publisher.AzureServiceBus` | Publish events to Azure Service Bus | [![NuGet](https://img.shields.io/nuget/v/Deveel.Events.Publisher.AzureServiceBus.svg)](https://www.nuget.org/packages/Deveel.Events.Publisher.AzureServiceBus) | [![GitHub pre-release](https://img.shields.io/nuget/vpre/Deveel.Events.Publisher.AzureServiceBus.svg?label=pre-release)](https://github.com/orgs/deveel/packages/nuget/package/Deveel.Events.Publisher.AzureServiceBus) | [![NuGet Downloads](https://img.shields.io/nuget/dt/Deveel.Events.Publisher.AzureServiceBus.svg)](https://www.nuget.org/packages/Deveel.Events.Publisher.AzureServiceBus) |
| `Deveel.Events.Amqp.Annotations` | AMQP-specific routing attributes (exchange, routing key) | [![NuGet](https://img.shields.io/nuget/v/Deveel.Events.Amqp.Annotations.svg)](https://www.nuget.org/packages/Deveel.Events.Amqp.Annotations) | [![GitHub pre-release](https://img.shields.io/nuget/vpre/Deveel.Events.Amqp.Annotations.svg?label=pre-release)](https://github.com/orgs/deveel/packages/nuget/package/Deveel.Events.Amqp.Annotations) | [![NuGet Downloads](https://img.shields.io/nuget/dt/Deveel.Events.Amqp.Annotations.svg)](https://www.nuget.org/packages/Deveel.Events.Amqp.Annotations) |
| `Deveel.Events.Publisher.RabbitMq` | Publish events to a RabbitMQ exchange | [![NuGet](https://img.shields.io/nuget/v/Deveel.Events.Publisher.RabbitMq.svg)](https://www.nuget.org/packages/Deveel.Events.Publisher.RabbitMq) | [![GitHub pre-release](https://img.shields.io/nuget/vpre/Deveel.Events.Publisher.RabbitMq.svg?label=pre-release)](https://github.com/orgs/deveel/packages/nuget/package/Deveel.Events.Publisher.RabbitMq) | [![NuGet Downloads](https://img.shields.io/nuget/dt/Deveel.Events.Publisher.RabbitMq.svg)](https://www.nuget.org/packages/Deveel.Events.Publisher.RabbitMq) |
| `Deveel.Events.Publisher.MassTransit` | Publish events through a MassTransit bus | [![NuGet](https://img.shields.io/nuget/v/Deveel.Events.Publisher.MassTransit.svg)](https://www.nuget.org/packages/Deveel.Events.Publisher.MassTransit) | [![GitHub pre-release](https://img.shields.io/nuget/vpre/Deveel.Events.Publisher.MassTransit.svg?label=pre-release)](https://github.com/orgs/deveel/packages/nuget/package/Deveel.Events.Publisher.MassTransit) | [![NuGet Downloads](https://img.shields.io/nuget/dt/Deveel.Events.Publisher.MassTransit.svg)](https://www.nuget.org/packages/Deveel.Events.Publisher.MassTransit) |
| `Deveel.Events.Publisher.Webhook` | Deliver events to HTTP webhook endpoints | [![NuGet](https://img.shields.io/nuget/v/Deveel.Events.Publisher.Webhook.svg)](https://www.nuget.org/packages/Deveel.Events.Publisher.Webhook) | [![GitHub pre-release](https://img.shields.io/nuget/vpre/Deveel.Events.Publisher.Webhook.svg?label=pre-release)](https://github.com/orgs/deveel/packages/nuget/package/Deveel.Events.Publisher.Webhook) | [![NuGet Downloads](https://img.shields.io/nuget/dt/Deveel.Events.Publisher.Webhook.svg)](https://www.nuget.org/packages/Deveel.Events.Publisher.Webhook) |

### Subscriptions

| Package | Description | NuGet (Stable) | Pre-release | Downloads |
|---------|-------------|---------------|-------------|-----------|
| `Deveel.Events.Subscriptions` | Event dispatcher and subscription management with pluggable resolvers | [![NuGet](https://img.shields.io/nuget/v/Deveel.Events.Subscriptions.svg)](https://www.nuget.org/packages/Deveel.Events.Subscriptions) | [![GitHub pre-release](https://img.shields.io/nuget/vpre/Deveel.Events.Subscriptions.svg?label=pre-release)](https://github.com/orgs/deveel/packages/nuget/package/Deveel.Events.Subscriptions) | [![NuGet Downloads](https://img.shields.io/nuget/dt/Deveel.Events.Subscriptions.svg)](https://www.nuget.org/packages/Deveel.Events.Subscriptions) |

### Schema

| Package | Description | NuGet (Stable) | Pre-release | Downloads |
|---------|-------------|---------------|-------------|-----------|
| `Deveel.Events.Schema` | Schema model, fluent builder, JSON writer, and validation | [![NuGet](https://img.shields.io/nuget/v/Deveel.Events.Schema.svg)](https://www.nuget.org/packages/Deveel.Events.Schema) | [![GitHub pre-release](https://img.shields.io/nuget/vpre/Deveel.Events.Schema.svg?label=pre-release)](https://github.com/orgs/deveel/packages/nuget/package/Deveel.Events.Schema) | [![NuGet Downloads](https://img.shields.io/nuget/dt/Deveel.Events.Schema.svg)](https://www.nuget.org/packages/Deveel.Events.Schema) |
| `Deveel.Events.Schema.Yaml` | Export an event schema as YAML | [![NuGet](https://img.shields.io/nuget/v/Deveel.Events.Schema.Yaml.svg)](https://www.nuget.org/packages/Deveel.Events.Schema.Yaml) | [![GitHub pre-release](https://img.shields.io/nuget/vpre/Deveel.Events.Schema.Yaml.svg?label=pre-release)](https://github.com/orgs/deveel/packages/nuget/package/Deveel.Events.Schema.Yaml) | [![NuGet Downloads](https://img.shields.io/nuget/dt/Deveel.Events.Schema.Yaml.svg)](https://www.nuget.org/packages/Deveel.Events.Schema.Yaml) |
| `Deveel.Events.Schema.AsyncApi` | Export schemas as an AsyncAPI 2.x document | [![NuGet](https://img.shields.io/nuget/v/Deveel.Events.Schema.AsyncApi.svg)](https://www.nuget.org/packages/Deveel.Events.Schema.AsyncApi) | [![GitHub pre-release](https://img.shields.io/nuget/vpre/Deveel.Events.Schema.AsyncApi.svg?label=pre-release)](https://github.com/orgs/deveel/packages/nuget/package/Deveel.Events.Schema.AsyncApi) | [![NuGet Downloads](https://img.shields.io/nuget/dt/Deveel.Events.Schema.AsyncApi.svg)](https://www.nuget.org/packages/Deveel.Events.Schema.AsyncApi) |

## Documentation

Full documentation — installation, quick-start, concept guides, channel references, schema export, and testing — is available in the [`docs/`](docs/README.md) folder of this repository.

| Section | Description |
|---------|-------------|
| [Getting Started](docs/getting-started/installation.md) | Installation and quick-start guide |
| [Core Concepts](docs/concepts/README.md) | Publisher, channels, and event annotations |
| [Publisher Channels](docs/publishers/README.md) | Azure Service Bus, RabbitMQ, MassTransit, Webhook |
| [Event Subscriptions](docs/subscriptions/README.md) | Event dispatcher, filters, routing, and custom resolvers |
| [Event Schema](docs/schema/README.md) | Schema definition, export (JSON / YAML / AsyncAPI), and validation |
| [Testing](docs/testing/README.md) | Unit-testing event publishing |

## Future Work

The framework is still evolving. See the [ROADMAP](ROADMAP.md) for the full list of planned features and the version milestone in which each is expected to ship.

### v1.x — Publisher & Schema maturity

- [x] **Event Subscription & Routing** *(v1.1)* — subscribe to event types with attribute-based filtering
- [ ] **Event Middleware Pipeline** *(v1.1)* — composable cross-cutting hooks (logging, validation, tracing)
- [ ] **Dead-Letter Handling & Replay** *(v1.2)* — capture and resubmit failed events
- [ ] **Outbox Pattern** *(v1.2)* — guaranteed exactly-once publishing via a transactional outbox
- [ ] **Event Scheduler** *(v1.2)* — defer event publishing to a future time or after a delay
- [ ] **OpenTelemetry Integration** *(v1.3)* — end-to-end distributed tracing across service boundaries
- [ ] **Event Store & Audit Log** *(v1.3)* — append-only persistence of domain events for auditing and read-model rebuilding
- [ ] **Publish Delivery Log** *(v1.3)* — per-attempt operational record of every publish (channel, outcome, error code, latency, retry count) across pluggable storage backends (SQL, file, in-memory)
- [ ] **Schema Validation at Publish Time** *(v1.3)* — validate events against their registered schema before dispatch
- [ ] **Event Versioning & Compatibility** *(v1.4)* — breaking-change detection and upcasting
- [ ] **AsyncAPI / Schema Export Improvements** *(v1.4)* — assembly scanning, CLI tooling, OpenAPI 3.1 webhooks
- [ ] **HTTP & gRPC Channels** *(v1.5)* — direct service-to-service delivery without a broker

### v2.x — Event Consumers

- [ ] **Webhook Consumer for ASP.NET Core** *(v2.0)* — receive inbound CloudEvents over HTTP with signature verification and automatic routing
- [ ] **RabbitMQ Consumer** *(v2.0)* — consume CloudEvents from RabbitMQ queues and route them through the subscription registry
- [ ] **Azure Service Bus Consumer** *(v2.0)* — consume CloudEvents from Service Bus queues and topic subscriptions
- [ ] **MassTransit Consumer Bridge** *(v2.0)* — expose Deveel Events subscriptions as MassTransit consumers
- [ ] **Expanded Testing Utilities** *(v2.1)* — fluent publish assertions, in-memory event bus, and consumer-side test helpers

Monitor the [open issues](https://github.com/deveel/deveel.events/issues) to see what is being actively worked on.

## Contributors

Thanks go to all the people who have contributed to this project!

[![Contributors](https://contrib.rocks/image?repo=deveel/deveel.events)](https://github.com/deveel/deveel.events/graphs/contributors)

## Contributing

We welcome bug reports, feature requests, and pull requests. Please read the [Contributing Guidelines](docs/contributing.md) before submitting.

## License

Released under the [MIT License](LICENSE).

## Built With

<p>
  <a href="https://dotnet.microsoft.com"><img align="left" src="https://raw.githubusercontent.com/devicons/devicon/master/icons/dotnetcore/dotnetcore-original.svg" alt=".NET" width="48" height="48"/></a>&nbsp;&nbsp;
  <a href="https://github.com"><img align="left" src="https://raw.githubusercontent.com/devicons/devicon/master/icons/github/github-original.svg" alt="GitHub" width="48" height="48"/></a>&nbsp;&nbsp;
  <a href="https://www.jetbrains.com/rider/"><img align="left" src="https://raw.githubusercontent.com/devicons/devicon/master/icons/rider/rider-original.svg" alt="Rider" width="48" height="48"/></a>&nbsp;&nbsp;
  <a href="https://testcontainers.com"><img align="left" src="https://avatars.githubusercontent.com/u/13393021" alt="Testcontainers" width="48" height="48"/></a>
</p>
