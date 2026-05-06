[![GitHub License](https://img.shields.io/github/license/deveel/deveel.events)](https://github.com/deveel/deveel.events/blob/main/LICENSE)
[![GitHub Release](https://img.shields.io/github/v/release/deveel/deveel.events)](https://github.com/deveel/deveel.events/releases) [![GitHub Actions Workflow Status](https://img.shields.io/github/actions/workflow/status/deveel/deveel.events/cicd.yml?logo=github)](https://github.com/deveel/deveel.events/actions/workflows/cicd.yml)
[![Codecov](https://img.shields.io/codecov/c/github/deveel/deveel.events?logo=codecov)](https://codecov.io/gh/deveel/deveel.events)
[![.NET](https://img.shields.io/badge/-8%20%7C%209%20%7C%2010-512BD4?logo=dotnet)](https://dotnet.microsoft.com/download)
[![Documentation](https://img.shields.io/badge/docs-events.deveel.org-blue)](https://events.deveel.org)

# Deveel Events

**Deveel Events** is a lightweight, extensible framework for building event-driven .NET applications, built on top of the [CloudEvents](https://cloudevents.io/) standard.

The ambition of this framework is to implement a set of common patterns and practices for a simple and efficient event-driven architecture in .NET — from publishing domain events to consuming them, managing subscriptions, and validating event schemas — without reinventing the wheel every time a team needs to integrate across bounded contexts.

The current release focuses on the **publishing side**: a transport-agnostic layer that broadcasts domain events from a bounded context to any number of downstream consumers. Upcoming releases will introduce consumer adapters, persistent subscription registries, and additional transport channels. See the [roadmap](ROADMAP.md) for the full plan.

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
| `Deveel.Events.Amqp.Annotations` | *(none — pure attribute library)* |
| `Deveel.Events.Publisher` | `CloudNative.CloudEvents` · `Microsoft.Extensions.Options` · `Microsoft.Extensions.Logging.Abstractions` |
| `Deveel.Events.Publisher.AzureServiceBus` | `Azure.Messaging.ServiceBus` ≥ 7.20 |
| `Deveel.Events.Publisher.DeadLetter` | `Deveel.Events.Publisher` · `Microsoft.Extensions.Hosting.Abstractions` |
| `Deveel.Events.Publisher.DeadLetter.EntityFramework` | `Deveel.Events.Publisher.DeadLetter` · `Microsoft.EntityFrameworkCore` |
| `Deveel.Events.Publisher.RabbitMq` | `RabbitMQ.Client` ≥ 7.2 · `Deveel.Events.Amqp.Annotations` |
| `Deveel.Events.Publisher.MassTransit` | `MassTransit` ≥ 9.1 |
| `Deveel.Events.Publisher.Webhook` | `Microsoft.Extensions.Http.Resilience` ≥ 9.6 |
| `Deveel.Events.Subscriptions` | `Deveel.Events.Publisher` · `Deveel.Filters` · `Microsoft.Extensions.Logging.Abstractions` |
| `Deveel.Events.Schema` | `CloudNative.CloudEvents` |
| `Deveel.Events.Schema.Yaml` | `YamlDotNet` ≥ 16.3 |
| `Deveel.Events.Schema.AsyncApi` | `Saunter` ≥ 0.13 · `YamlDotNet` ≥ 16.3 · ASP.NET Core shared framework |

## Packages

### Publishing

| Package | Description | NuGet (Stable) | GitHub (Unstable)                                                                                                                                                                                                       | Downloads |
|---------|-------------|---------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|-----------|
| `Deveel.Events.Annotations` | Attributes for describing event metadata on data classes | [![NuGet](https://img.shields.io/nuget/v/Deveel.Events.Annotations.svg)](https://www.nuget.org/packages/Deveel.Events.Annotations) | [![GitHub pre-release](https://img.shields.io/nuget/vpre/Deveel.Events.Annotations.svg?label=pre-release)](https://github.com/orgs/deveel/packages/nuget/package/Deveel.Events.Annotations)                             | [![NuGet Downloads](https://img.shields.io/nuget/dt/Deveel.Events.Annotations.svg)](https://www.nuget.org/packages/Deveel.Events.Annotations) |
| `Deveel.Events.Publisher` | Core publisher infrastructure (`EventPublisher`, DI helpers) | [![NuGet](https://img.shields.io/nuget/v/Deveel.Events.Publisher.svg)](https://www.nuget.org/packages/Deveel.Events.Publisher) | [![GitHub pre-release](https://img.shields.io/nuget/vpre/Deveel.Events.Publisher.svg?label=pre-release)](https://github.com/orgs/deveel/packages/nuget/package/Deveel.Events.Publisher)                                 | [![NuGet Downloads](https://img.shields.io/nuget/dt/Deveel.Events.Publisher.svg)](https://www.nuget.org/packages/Deveel.Events.Publisher) |
| `Deveel.Events.Publisher.AzureServiceBus` | Publish events to Azure Service Bus | [![NuGet](https://img.shields.io/nuget/v/Deveel.Events.Publisher.AzureServiceBus.svg)](https://www.nuget.org/packages/Deveel.Events.Publisher.AzureServiceBus) | [![GitHub pre-release](https://img.shields.io/nuget/vpre/Deveel.Events.Publisher.AzureServiceBus.svg?label=pre-release)](https://github.com/orgs/deveel/packages/nuget/package/Deveel.Events.Publisher.AzureServiceBus) | [![NuGet Downloads](https://img.shields.io/nuget/dt/Deveel.Events.Publisher.AzureServiceBus.svg)](https://www.nuget.org/packages/Deveel.Events.Publisher.AzureServiceBus) |
| `Deveel.Events.Publisher.DeadLetter` | Dead-letter handling, persistent replay abstractions, and replay workers | [![NuGet](https://img.shields.io/nuget/v/Deveel.Events.Publisher.DeadLetter.svg)](https://www.nuget.org/packages/Deveel.Events.Publisher.DeadLetter) | [![GitHub pre-release](https://img.shields.io/nuget/vpre/Deveel.Events.Publisher.DeadLetter.svg?label=pre-release)](https://github.com/orgs/deveel/packages/nuget/package/Deveel.Events.Publisher.DeadLetter) | [![NuGet Downloads](https://img.shields.io/nuget/dt/Deveel.Events.Publisher.DeadLetter.svg)](https://www.nuget.org/packages/Deveel.Events.Publisher.DeadLetter) |
| `Deveel.Events.Publisher.DeadLetter.EntityFramework` | Entity Framework Core persistence for dead-letter messages | [![NuGet](https://img.shields.io/nuget/v/Deveel.Events.Publisher.DeadLetter.EntityFramework.svg)](https://www.nuget.org/packages/Deveel.Events.Publisher.DeadLetter.EntityFramework) | [![GitHub pre-release](https://img.shields.io/nuget/vpre/Deveel.Events.Publisher.DeadLetter.EntityFramework.svg?label=pre-release)](https://github.com/orgs/deveel/packages/nuget/package/Deveel.Events.Publisher.DeadLetter.EntityFramework) | [![NuGet Downloads](https://img.shields.io/nuget/dt/Deveel.Events.Publisher.DeadLetter.EntityFramework.svg)](https://www.nuget.org/packages/Deveel.Events.Publisher.DeadLetter.EntityFramework) |
| `Deveel.Events.Amqp.Annotations` | AMQP-specific routing attributes (exchange, routing key) | [![NuGet](https://img.shields.io/nuget/v/Deveel.Events.Amqp.Annotations.svg)](https://www.nuget.org/packages/Deveel.Events.Amqp.Annotations) | [![GitHub pre-release](https://img.shields.io/nuget/vpre/Deveel.Events.Amqp.Annotations.svg?label=pre-release)](https://github.com/orgs/deveel/packages/nuget/package/Deveel.Events.Amqp.Annotations)                   | [![NuGet Downloads](https://img.shields.io/nuget/dt/Deveel.Events.Amqp.Annotations.svg)](https://www.nuget.org/packages/Deveel.Events.Amqp.Annotations) |
| `Deveel.Events.Publisher.RabbitMq` | Publish events to a RabbitMQ exchange | [![NuGet](https://img.shields.io/nuget/v/Deveel.Events.Publisher.RabbitMq.svg)](https://www.nuget.org/packages/Deveel.Events.Publisher.RabbitMq) | [![GitHub pre-release](https://img.shields.io/nuget/vpre/Deveel.Events.Publisher.RabbitMq.svg?label=pre-release)](https://github.com/orgs/deveel/packages/nuget/package/Deveel.Events.Publisher.RabbitMq)               | [![NuGet Downloads](https://img.shields.io/nuget/dt/Deveel.Events.Publisher.RabbitMq.svg)](https://www.nuget.org/packages/Deveel.Events.Publisher.RabbitMq) |
| `Deveel.Events.Publisher.MassTransit` | Publish events through a MassTransit bus | [![NuGet](https://img.shields.io/nuget/v/Deveel.Events.Publisher.MassTransit.svg)](https://www.nuget.org/packages/Deveel.Events.Publisher.MassTransit) | [![GitHub pre-release](https://img.shields.io/nuget/vpre/Deveel.Events.Publisher.MassTransit.svg?label=pre-release)](https://github.com/orgs/deveel/packages/nuget/package/Deveel.Events.Publisher.MassTransit)         | [![NuGet Downloads](https://img.shields.io/nuget/dt/Deveel.Events.Publisher.MassTransit.svg)](https://www.nuget.org/packages/Deveel.Events.Publisher.MassTransit) |
| `Deveel.Events.Publisher.Webhook` | Deliver events to HTTP webhook endpoints | [![NuGet](https://img.shields.io/nuget/v/Deveel.Events.Publisher.Webhook.svg)](https://www.nuget.org/packages/Deveel.Events.Publisher.Webhook) | [![GitHub pre-release](https://img.shields.io/nuget/vpre/Deveel.Events.Publisher.Webhook.svg?label=pre-release)](https://github.com/orgs/deveel/packages/nuget/package/Deveel.Events.Publisher.Webhook)                 | [![NuGet Downloads](https://img.shields.io/nuget/dt/Deveel.Events.Publisher.Webhook.svg)](https://www.nuget.org/packages/Deveel.Events.Publisher.Webhook) |

### Subscriptions

| Package | Description | NuGet (Stable) | GitHub (Unstable)                                                                                                                                                                               | Downloads |
|---------|-------------|---------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|-----------|
| `Deveel.Events.Subscriptions` | Event dispatcher and subscription management with pluggable resolvers | [![NuGet](https://img.shields.io/nuget/v/Deveel.Events.Subscriptions.svg)](https://www.nuget.org/packages/Deveel.Events.Subscriptions) | [![GitHub pre-release](https://img.shields.io/nuget/vpre/Deveel.Events.Subscriptions.svg?label=pre-release)](https://github.com/orgs/deveel/packages/nuget/package/Deveel.Events.Subscriptions) | [![NuGet Downloads](https://img.shields.io/nuget/dt/Deveel.Events.Subscriptions.svg)](https://www.nuget.org/packages/Deveel.Events.Subscriptions) |

### Schema

| Package | Description | NuGet (Stable) | GitHub (Unstable)                                                                                                                                                                                   | Downloads |
|---------|-------------|---------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|-----------|
| `Deveel.Events.Schema` | Schema model, fluent builder, JSON writer, and validation | [![NuGet](https://img.shields.io/nuget/v/Deveel.Events.Schema.svg)](https://www.nuget.org/packages/Deveel.Events.Schema) | [![GitHub pre-release](https://img.shields.io/nuget/vpre/Deveel.Events.Schema.svg?label=pre-release)](https://github.com/orgs/deveel/packages/nuget/package/Deveel.Events.Schema)                   | [![NuGet Downloads](https://img.shields.io/nuget/dt/Deveel.Events.Schema.svg)](https://www.nuget.org/packages/Deveel.Events.Schema) |
| `Deveel.Events.Schema.Yaml` | Export an event schema as YAML | [![NuGet](https://img.shields.io/nuget/v/Deveel.Events.Schema.Yaml.svg)](https://www.nuget.org/packages/Deveel.Events.Schema.Yaml) | [![GitHub pre-release](https://img.shields.io/nuget/vpre/Deveel.Events.Schema.Yaml.svg?label=pre-release)](https://github.com/orgs/deveel/packages/nuget/package/Deveel.Events.Schema.Yaml)         | [![NuGet Downloads](https://img.shields.io/nuget/dt/Deveel.Events.Schema.Yaml.svg)](https://www.nuget.org/packages/Deveel.Events.Schema.Yaml) |
| `Deveel.Events.Schema.AsyncApi` | Export schemas as an AsyncAPI 2.x document | [![NuGet](https://img.shields.io/nuget/v/Deveel.Events.Schema.AsyncApi.svg)](https://www.nuget.org/packages/Deveel.Events.Schema.AsyncApi) | [![GitHub pre-release](https://img.shields.io/nuget/vpre/Deveel.Events.Schema.AsyncApi.svg?label=pre-release)](https://github.com/orgs/deveel/packages/nuget/package/Deveel.Events.Schema.AsyncApi) | [![NuGet Downloads](https://img.shields.io/nuget/dt/Deveel.Events.Schema.AsyncApi.svg)](https://www.nuget.org/packages/Deveel.Events.Schema.AsyncApi) |

## Documentation

The full documentation is published at **[events.deveel.org](https://events.deveel.org)** — including installation guides, concept references, channel adapters, schema export, and testing utilities.

The source of the documentation is also available in the [`docs/`](docs/README.md) folder of this repository (published via GitBook).

| Section | Description |
|---------|-------------|
| [Getting Started](https://events.deveel.org/getting-started/installation) | Installation and quick-start guide |
| [Core Concepts](https://events.deveel.org/concepts/) | Publisher, channels, and event annotations |
| [Publisher Channels](https://events.deveel.org/publishers/) | Azure Service Bus, RabbitMQ, MassTransit, Webhook, Outbox, and Dead-Letter Replay |
| [Event Subscriptions](https://events.deveel.org/subscriptions/) | Event dispatcher, filters, routing, and custom resolvers |
| [Event Schema](https://events.deveel.org/schema/) | Schema definition, export (JSON / YAML / AsyncAPI), and validation |
| [Testing](https://events.deveel.org/testing/) | Unit-testing event publishing |

## Future Work

The framework is still evolving. See the [ROADMAP](ROADMAP.md) for the full description of every planned feature and the version milestone in which each is expected to ship.

### v1.1 — Routing & Middleware ✅

- [x] **Event Subscription & Routing** — subscribe to event types with attribute-based filtering and in-process routing
- [x] **Event Middleware Pipeline** — composable cross-cutting hooks (logging, validation, correlation, tracing)

### v1.2 — Reliability ✅

- [x] **Event Replay & Dead-Letter Handling** — capture and resubmit failed events with configurable retry and back-off
- [x] **Outbox Pattern Integration** — guaranteed exactly-once publishing via a transactional outbox channel
- [x] **Event Scheduler & Deferred Publishing** — defer event publishing to a future point in time or after a delay

### v1.3 — Observability

- [ ] **OpenTelemetry & Distributed Tracing Integration** — propagate W3C trace context as CloudEvents extensions for end-to-end traces
- [ ] **Event Store & Audit Log Channel** — append-only persistence of every domain event for auditing and read-model rebuilding
- [ ] **Schema Validation at Publish Time** — validate event payloads against their registered schema before channel dispatch
- [ ] **Publish Delivery Log** — per-attempt operational record (channel, outcome, error code, latency, retry count) across pluggable storage backends

### v1.4 — Schema Governance

- [ ] **Event Versioning & Compatibility** — breaking-change detection, upcasting pipeline, and version-aware routing
- [ ] **AsyncAPI & Schema Export Improvements** — compile-time auto-discovery, dotnet CLI tool, OpenAPI 3.1 webhook export

### v1.5 — New Transports

- [ ] **CloudEvents HTTP Binding for the Webhook Publisher** — bring the existing Webhook publisher into full structured/binary content-mode compliance
- [ ] **HTTP Publisher Channel** — lightweight, sign-free point-to-point delivery to statically-configured endpoints
- [ ] **gRPC Publisher Channel** — low-latency service-to-service delivery using the CloudEvents gRPC protocol binding
- [ ] **Apache Kafka Publisher Channel** — publish CloudEvents to Kafka topics with partition key control and Schema Registry support
- [ ] **Amazon SQS Publisher Channel** — standard and FIFO queue delivery on AWS with batch publish and S3 offload
- [ ] **Amazon SNS Publisher Channel** — fan-out to SQS queues, Lambda functions, and HTTP endpoints via SNS topics
- [ ] **Google Cloud Pub/Sub Publisher Channel** — ordered delivery to GCP Pub/Sub topics with Workload Identity support
- [ ] **NATS / JetStream Publisher Channel** — ultra-low-latency delivery to NATS subjects or durable JetStream streams

### v1.6 — Code Generation

- [ ] **CloudEvent Factory Source Generator** — Roslyn incremental generator that emits zero-reflection `IEventConvertible.ToCloudEvent()` implementations from `[Event]`-annotated `partial` classes, with compile-time diagnostics for annotation mistakes
- [ ] **Schema Registration Source Generator** — generator that pre-constructs all `EventSchema` instances at build time and emits an `AddGeneratedEventSchemas(IServiceCollection)` DI extension, eliminating startup reflection and enabling schema export without a running host
- [ ] **Typed Domain Publisher Generator** — generator that produces a strongly-typed `IXxxEventPublisher` interface and implementation per domain group, so services depend on a focused, mockable contract rather than the catch-all `IEventPublisher`

### v2.0 — Event Consumers

- [ ] **Webhook Consumer for ASP.NET Core** — receive inbound CloudEvents over HTTP with HMAC signature verification and automatic routing
- [ ] **Pre-built Webhook Consumer Adapters** — ready-made payload mappers and signature verifiers for Facebook, SendGrid, Twilio, Stripe, GitHub, and Shopify
- [ ] **RabbitMQ Consumer** — consume CloudEvents from RabbitMQ queues and route them through the subscription registry
- [ ] **Azure Service Bus Consumer** — consume CloudEvents from Service Bus queues and topic subscriptions
- [ ] **MassTransit Consumer Bridge** — expose Deveel Events subscriptions as MassTransit consumers and vice versa

### v2.1 — Testing & DX

- [ ] **Expanded Testing Utilities** — fluent publish assertions (`AssertPublished`, `AssertNotPublished`), in-memory event bus, and consumer-side test helpers
- [ ] **Local Development Console Sink** — zero-configuration channel that pretty-prints CloudEvents to the console during local development, with automatic exclusion in non-development environments
- [ ] **.NET Aspire Integration** — surface publish channels as Aspire resources for dashboard visibility, automatic broker provisioning, and OTLP trace export out of the box
- [ ] **`dotnet event` CLI Extension** — `dotnet` global tool adding `dotnet event new`, `schema export`, `schema validate`, `schema diff`, and `channel add` sub-commands for event scaffolding and schema governance
- [ ] **Standalone `deveel-events` CLI** — self-contained cross-platform executable and Docker image exposing the same command surface without requiring the .NET SDK, with GitHub Actions action and machine-readable SARIF output

### v2.2 — Subscription Management

- [ ] **Subscription Management Framework** — provider-agnostic `ISubscriptionStore` abstraction with in-memory default and runtime lifecycle operations
- [ ] **Relational Registry Provider (Entity Framework Core)** — persist subscriptions in SQL Server, PostgreSQL, or SQLite with bundled migrations
- [ ] **Document Registry Provider (MongoDB)** — persist subscriptions as MongoDB documents with real-time change-stream synchronisation
- [ ] **Subscription Management REST API** — secured minimal-API endpoint group with OpenAPI metadata and change-notification webhooks

### v2.3 — Framework Integrations

- [ ] **MediatR Integration** — bridge `[Event]`-annotated `INotification` types to the CloudEvents publish pipeline and route inbound CloudEvents back as MediatR notifications
- [ ] **Wolverine Integration** — emit CloudEvents as a Wolverine message side-effect and route inbound CloudEvents into the Wolverine runtime via `IMessageBus`
- [ ] **Brighter Integration** — publish CloudEvents as a post-handler pipeline step for Brighter `ICommand` / `IEvent` types and bridge inbound CloudEvents into the Brighter command processor

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
