<p align="center">
  <img src="hermodr-full-logo.png" alt="Hermodr" width="400"/>
</p>

[![GitHub License](https://img.shields.io/github/license/deveel/hermodr)](https://github.com/deveel/hermodr/blob/main/LICENSE)
[![GitHub Release](https://img.shields.io/github/v/release/deveel/hermodr)](https://github.com/deveel/hermodr/releases) [![GitHub Actions Workflow Status](https://img.shields.io/github/actions/workflow/status/deveel/hermodr/cicd.yml?logo=github)](https://github.com/deveel/hermodr/actions/workflows/cicd.yml)
[![Codecov](https://img.shields.io/codecov/c/github/deveel/hermodr?logo=codecov)](https://codecov.io/gh/deveel/hermodr)
[![.NET](https://img.shields.io/badge/-8%20%7C%209%20%7C%2010-512BD4?logo=dotnet)](https://dotnet.microsoft.com/download)
[![Documentation](https://img.shields.io/badge/docs-hermodr.deveel.org-blue)](https://hermodr.deveel.org)

> **Renamed:** This project was formerly called **Deveel Events** - as of **23 May 2026**, it has been renamed to **Hermodr** — named after the [messenger of the gods](https://no.wikipedia.org/wiki/Hermod) in Norse mythology — to better reflect its role as a message delivery framework and to distinguish it from the broader [Deveel](https://github.com/deveel) ecosystem.
> Existing NuGet packages (`Deveel.Events.*`) remain published and will be deprecated in favor of the new `Hermodr.*` packages.

**Hermodr** is a lightweight, extensible framework for building event-driven .NET applications, built on top of the [CloudEvents](https://cloudevents.io/) standard.

The ambition of this framework is to implement a set of common patterns and practices for a simple and efficient event-driven architecture in .NET — from publishing domain events to consuming them, managing subscriptions, and validating event schemas — without reinventing the wheel every time a team needs to integrate across bounded contexts.

The current release focuses on the **publishing side**: a transport-agnostic layer that broadcasts domain events from a bounded context to any number of downstream consumers. Upcoming releases will introduce consumer adapters, persistent subscription registries, and additional transport channels. See the [roadmap](ROADMAP.md) for the full plan.

## Domain Events and DDD

[Domain-Driven Design (DDD)](https://martinfowler.com/bliki/DomainDrivenDesign.html) treats **domain events** as first-class citizens of the model — facts about something that _happened_ inside the domain, named in the ubiquitous language of domain experts (`OrderPlaced`, `InvoiceIssued`, `UserRegistered`).

Key properties of domain events:

- **Immutable facts** — they describe what happened, not what should happen.
- **Decoupled producers and consumers** — the producing bounded context does not need to know who is listening.
- **Cross-context integration** — events are the preferred way to share information across bounded contexts without tight coupling.
- **Temporal decoupling** — consumers can process events asynchronously, at their own pace.

Hermodr implements the **publishing side** of this pattern: a transport-agnostic layer that broadcasts domain events from a bounded context to any number of downstream consumers, without prescribing how you model aggregates or build read models.

## Event Schemas as Async API Contracts

Publishing an event is only half the story. Consumers need to know the **shape** of each event — its properties, types, and constraints — to deserialise it correctly and build reliable integrations. Without a formal contract, field renames silently break consumers and integration knowledge lives only in tribal memory.

**Event schemas** fill the same role for asynchronous messaging that OpenAPI/Swagger fills for REST APIs. The `Hermodr.Schema` package can derive a schema automatically from annotated data classes and export it as:

- **JSON Schema** — for schema-registry integration and tooling.
- **YAML** — for human-readable, version-controlled contract documents.
- **AsyncAPI 2.x** — a complete, machine-readable async API specification. AsyncAPI tooling can generate documentation sites, client SDKs, and mock servers from it.

Treat schemas as public API contracts: version them, prefer additive changes, and communicate breaking changes in advance.

## Motivation

Applications frequently need to notify other parts of the system about domain events. Teams end up rewriting the same boilerplate — serialising payloads, constructing envelopes, wiring up transport clients — over and over again.

Hermodr provides a single, consistent way to publish events across any transport, so teams can focus on domain logic instead of infrastructure plumbing.

## Hermodr vs .NET Messaging Frameworks

Hermodr does not try to compete with other messaging frameworks in the .NET ecosystem: these frameworks solve different problems and can be complementary in the same architecture. The table below is a quick positioning guide, not a ranking.

| Feature | `Hermodr` | `MassTransit` | `Wolverine` | `NServiceBus` | `Rebus` |
|---------|-----------|---------------|--------------|---------------|-----------|
| Event contract model | CloudEvents-first publish pipeline | Framework-native message contracts; CloudEvents not natively provided by the framework core | Framework-native message contracts; CloudEvents not natively provided by the framework core | Framework-native message contracts; CloudEvents not natively provided by the framework core | Framework-native message contracts; CloudEvents not natively provided by the framework core |
| Event metadata annotations | Built-in attributes (`Hermodr.Annotations`, AMQP extensions) | Event metadata annotations not natively provided by the framework core | Event metadata annotations not natively provided by the framework core | Event metadata annotations not natively provided by the framework core | Event metadata annotations not natively provided by the framework core |
| Schema export formats | JSON Schema, YAML, AsyncAPI packages | Schema export not natively provided by the framework core | Schema export not natively provided by the framework core | Schema export not natively provided by the framework core | Schema export not natively provided by the framework core |
| AsyncAPI generation focus | Dedicated package (`Hermodr.Schema.AsyncApi`) | AsyncAPI generation not natively provided by the framework core | AsyncAPI generation not natively provided by the framework core | AsyncAPI generation not natively provided by the framework core | AsyncAPI generation not natively provided by the framework core |
| Transport adapters included | Azure Service Bus, RabbitMQ, MassTransit, Webhook, Outbox, Dead-Letter | Native multi-transport broker integrations | Native multi-transport messaging endpoints | Native transport support via transport packages | Native transport integrations |
| Transactional outbox support | Built-in channel + EF integration packages | Natively supported | Natively supported | Natively supported | Natively supported |
| Dead-letter capture and replay | Dedicated dead-letter packages + replay worker model | Dead-letter handling available; replay workflow not natively standardized by the framework core | Dead-letter handling available; replay workflow not natively standardized by the framework core | Dead-letter handling available; replay workflow not natively standardized by the framework core | Dead-letter handling available; replay workflow not natively standardized by the framework core |
| Deferred/scheduled delivery | Planned (`Event Scheduler & Deferred Publishing` on roadmap) | Natively supported (transport/scheduler dependent) | Natively supported (runtime/transport dependent) | Natively supported (transport dependent) | Natively supported (transport dependent) |
| In-process subscription routing | Built-in subscriptions package (`Hermodr.Subscriptions`) | Native consumer/handler pipeline | Native local and remote handlers | Native message handler pipeline | Native message handler pipeline |
| Middleware/extensibility pipeline | Built-in event middleware pipeline | Native filters/middleware/observers | Native middleware and handler pipeline extensions | Native pipeline behaviors and extensibility points | Native pipeline steps and extensibility points |
| Testing support for publish flow | Dedicated in-memory test publisher package | Native test harness support | Native testing utilities | Native testing support | Native testing support |

Capabilities evolve quickly across all projects, so validate details against each framework's current documentation.

## CloudEvents Standard

All events are modelled as [`CloudEvent`](https://cloudevents.io/) objects, ensuring maximum interoperability with cloud platforms and services that implement the CNCF CloudEvents specification.

## Requirements

All packages in this solution multi-target the following runtimes:

| Runtime | Version |
|---------|---------|
| .NET | 8, 9, 10 |

> **Note:** `Hermodr.Schema.AsyncApi` also requires the **ASP.NET Core** shared framework (`Microsoft.AspNetCore.App`), since it integrates with the Saunter AsyncAPI middleware.

Every package requires the **Microsoft Dependency Injection** infrastructure (`Microsoft.Extensions.DependencyInjection`). Below are the additional per-package dependencies automatically pulled in as transitive NuGet references:

| Package | Key Dependencies |
|---------|-----------------|
| `Hermodr.Annotations` | *(none — pure attribute library)* |
| `Hermodr.Amqp.Annotations` | *(none — pure attribute library)* |
| `Hermodr.Publisher` | `CloudNative.CloudEvents` · `Microsoft.Extensions.Options` · `Microsoft.Extensions.Logging.Abstractions` |
| `Hermodr.Publisher.AzureServiceBus` | `Azure.Messaging.ServiceBus` ≥ 7.20 |
| `Hermodr.Publisher.DeadLetter` | `Hermodr.Publisher` · `Microsoft.Extensions.Hosting.Abstractions` |
| `Hermodr.Publisher.DeadLetter.EntityFramework` | `Hermodr.Publisher.DeadLetter` · `Microsoft.EntityFrameworkCore` |
| `Hermodr.Publisher.RabbitMq` | `RabbitMQ.Client` ≥ 7.2 · `Hermodr.Amqp.Annotations` |
| `Hermodr.Publisher.MassTransit` | `MassTransit` ≥ 9.1 |
| `Hermodr.Publisher.Webhook` | `Microsoft.Extensions.Http.Resilience` ≥ 9.6 |
| `Hermodr.Publisher.Outbox` | `Deveel.Repository.Manager` · `Microsoft.Extensions.Hosting.Abstractions` |
| `Hermodr.Publisher.Outbox.EntityFramework` | `Hermodr.Publisher.Outbox` · `Deveel.Repository.EntityFramework` · `Microsoft.EntityFrameworkCore.Relational` |
| `Hermodr.Subscriptions` | `Hermodr.Publisher` · `Deveel.Filters` · `Microsoft.Extensions.Logging.Abstractions` |
| `Hermodr.Schema` | `CloudNative.CloudEvents` |
| `Hermodr.Schema.Yaml` | `YamlDotNet` ≥ 16.3 |
| `Hermodr.Schema.AsyncApi` | `Saunter` ≥ 0.13 · `YamlDotNet` ≥ 16.3 · ASP.NET Core shared framework |
| `Hermodr.TestPublisher` | `Hermodr.Publisher` |

## Packages

### Publishing

| Package | Description | NuGet (Stable) | GitHub (Unstable) |
|---------|-------------|---------------|-------------------|
| `Hermodr.Annotations` | Attributes for describing event metadata on data classes | [![NuGet](https://img.shields.io/nuget/v/Hermodr.Annotations.svg)](https://www.nuget.org/packages/Hermodr.Annotations) | [![GitHub pre-release](https://img.shields.io/nuget/vpre/Hermodr.Annotations.svg?label=pre-release)](https://github.com/orgs/deveel/packages/nuget/package/Hermodr.Annotations) |
| `Hermodr.Publisher` | Core publisher infrastructure (`EventPublisher`, DI helpers) | [![NuGet](https://img.shields.io/nuget/v/Hermodr.Publisher.svg)](https://www.nuget.org/packages/Hermodr.Publisher) | [![GitHub pre-release](https://img.shields.io/nuget/vpre/Hermodr.Publisher.svg?label=pre-release)](https://github.com/orgs/deveel/packages/nuget/package/Hermodr.Publisher) |
| `Hermodr.Publisher.AzureServiceBus` | Publish events to Azure Service Bus | [![NuGet](https://img.shields.io/nuget/v/Hermodr.Publisher.AzureServiceBus.svg)](https://www.nuget.org/packages/Hermodr.Publisher.AzureServiceBus) | [![GitHub pre-release](https://img.shields.io/nuget/vpre/Hermodr.Publisher.AzureServiceBus.svg?label=pre-release)](https://github.com/orgs/deveel/packages/nuget/package/Hermodr.Publisher.AzureServiceBus) |
| `Hermodr.Publisher.DeadLetter` | Dead-letter handling, persistent replay abstractions, and replay workers | [![NuGet](https://img.shields.io/nuget/v/Hermodr.Publisher.DeadLetter.svg)](https://www.nuget.org/packages/Hermodr.Publisher.DeadLetter) | [![GitHub pre-release](https://img.shields.io/nuget/vpre/Hermodr.Publisher.DeadLetter.svg?label=pre-release)](https://github.com/orgs/deveel/packages/nuget/package/Hermodr.Publisher.DeadLetter) |
| `Hermodr.Publisher.DeadLetter.EntityFramework` | Entity Framework Core persistence for dead-letter messages | [![NuGet](https://img.shields.io/nuget/v/Hermodr.Publisher.DeadLetter.EntityFramework.svg)](https://www.nuget.org/packages/Hermodr.Publisher.DeadLetter.EntityFramework) | [![GitHub pre-release](https://img.shields.io/nuget/vpre/Hermodr.Publisher.DeadLetter.EntityFramework.svg?label=pre-release)](https://github.com/orgs/deveel/packages/nuget/package/Hermodr.Publisher.DeadLetter.EntityFramework) |
| `Hermodr.Amqp.Annotations` | AMQP-specific routing attributes (exchange, routing key) | [![NuGet](https://img.shields.io/nuget/v/Hermodr.Amqp.Annotations.svg)](https://www.nuget.org/packages/Hermodr.Amqp.Annotations) | [![GitHub pre-release](https://img.shields.io/nuget/vpre/Hermodr.Amqp.Annotations.svg?label=pre-release)](https://github.com/orgs/deveel/packages/nuget/package/Hermodr.Amqp.Annotations) |
| `Hermodr.Publisher.RabbitMq` | Publish events to a RabbitMQ exchange | [![NuGet](https://img.shields.io/nuget/v/Hermodr.Publisher.RabbitMq.svg)](https://www.nuget.org/packages/Hermodr.Publisher.RabbitMq) | [![GitHub pre-release](https://img.shields.io/nuget/vpre/Hermodr.Publisher.RabbitMq.svg?label=pre-release)](https://github.com/orgs/deveel/packages/nuget/package/Hermodr.Publisher.RabbitMq) |
| `Hermodr.Publisher.MassTransit` | Publish events through a MassTransit bus | [![NuGet](https://img.shields.io/nuget/v/Hermodr.Publisher.MassTransit.svg)](https://www.nuget.org/packages/Hermodr.Publisher.MassTransit) | [![GitHub pre-release](https://img.shields.io/nuget/vpre/Hermodr.Publisher.MassTransit.svg?label=pre-release)](https://github.com/orgs/deveel/packages/nuget/package/Hermodr.Publisher.MassTransit) |
| `Hermodr.Publisher.Webhook` | Deliver events to HTTP webhook endpoints | [![NuGet](https://img.shields.io/nuget/v/Hermodr.Publisher.Webhook.svg)](https://www.nuget.org/packages/Hermodr.Publisher.Webhook) | [![GitHub pre-release](https://img.shields.io/nuget/vpre/Hermodr.Publisher.Webhook.svg?label=pre-release)](https://github.com/orgs/deveel/packages/nuget/package/Hermodr.Publisher.Webhook) |
| `Hermodr.Publisher.Outbox` | Persist events in a transactional outbox for later relay | [![NuGet](https://img.shields.io/nuget/v/Hermodr.Publisher.Outbox.svg)](https://www.nuget.org/packages/Hermodr.Publisher.Outbox) | [![GitHub pre-release](https://img.shields.io/nuget/vpre/Hermodr.Publisher.Outbox.svg?label=pre-release)](https://github.com/orgs/deveel/packages/nuget/package/Hermodr.Publisher.Outbox) |
| `Hermodr.Publisher.Outbox.EntityFramework` | Entity Framework Core repository and helpers for the outbox channel | [![NuGet](https://img.shields.io/nuget/v/Hermodr.Publisher.Outbox.EntityFramework.svg)](https://www.nuget.org/packages/Hermodr.Publisher.Outbox.EntityFramework) | [![GitHub pre-release](https://img.shields.io/nuget/vpre/Hermodr.Publisher.Outbox.EntityFramework.svg?label=pre-release)](https://github.com/orgs/deveel/packages/nuget/package/Hermodr.Publisher.Outbox.EntityFramework) |

### Subscriptions

| Package | Description | NuGet (Stable) | GitHub (Unstable) |
|---------|-------------|---------------|-------------------|
| `Hermodr.Subscriptions` | Event dispatcher and subscription management with pluggable resolvers | [![NuGet](https://img.shields.io/nuget/v/Hermodr.Subscriptions.svg)](https://www.nuget.org/packages/Hermodr.Subscriptions) | [![GitHub pre-release](https://img.shields.io/nuget/vpre/Hermodr.Subscriptions.svg?label=pre-release)](https://github.com/orgs/deveel/packages/nuget/package/Hermodr.Subscriptions) |

### Schema

| Package | Description | NuGet (Stable) | GitHub (Unstable) |
|---------|-------------|---------------|-------------------|
| `Hermodr.Schema` | Schema model, fluent builder, JSON writer, and validation | [![NuGet](https://img.shields.io/nuget/v/Hermodr.Schema.svg)](https://www.nuget.org/packages/Hermodr.Schema) | [![GitHub pre-release](https://img.shields.io/nuget/vpre/Hermodr.Schema.svg?label=pre-release)](https://github.com/orgs/deveel/packages/nuget/package/Hermodr.Schema) |
| `Hermodr.Schema.Yaml` | Export an event schema as YAML | [![NuGet](https://img.shields.io/nuget/v/Hermodr.Schema.Yaml.svg)](https://www.nuget.org/packages/Hermodr.Schema.Yaml) | [![GitHub pre-release](https://img.shields.io/nuget/vpre/Hermodr.Schema.Yaml.svg?label=pre-release)](https://github.com/orgs/deveel/packages/nuget/package/Hermodr.Schema.Yaml) |
| `Hermodr.Schema.AsyncApi` | Export schemas as an AsyncAPI 2.x document | [![NuGet](https://img.shields.io/nuget/v/Hermodr.Schema.AsyncApi.svg)](https://www.nuget.org/packages/Hermodr.Schema.AsyncApi) | [![GitHub pre-release](https://img.shields.io/nuget/vpre/Hermodr.Schema.AsyncApi.svg?label=pre-release)](https://github.com/orgs/deveel/packages/nuget/package/Hermodr.Schema.AsyncApi) |

### Testing

| Package | Description | NuGet (Stable) | GitHub (Unstable) |
|---------|-------------|---------------|-------------------|
| `Hermodr.TestPublisher` | In-memory test channel for unit and integration tests | [![NuGet](https://img.shields.io/nuget/v/Hermodr.TestPublisher.svg)](https://www.nuget.org/packages/Hermodr.TestPublisher) | [![GitHub pre-release](https://img.shields.io/nuget/vpre/Hermodr.TestPublisher.svg?label=pre-release)](https://github.com/orgs/deveel/packages/nuget/package/Hermodr.TestPublisher) |

## Documentation

The full documentation is published at **[hermodr.deveel.org](https://hermodr.deveel.org)** — including installation guides, concept references, channel adapters, schema export, and testing utilities.

The source of the documentation is also available in the [`docs/`](docs/README.md) folder of this repository
| Section | Description |
|---------|-------------|
| [Getting Started](https://hermodr.deveel.org/getting-started/installation) | Installation and quick-start guide |
| [Core Concepts](https://hermodr.deveel.org/concepts/) | Publisher, channels, and event annotations |
| [Publisher Channels](https://hermodr.deveel.org/publishers/) | Azure Service Bus, RabbitMQ, MassTransit, Webhook, Outbox, and Dead-Letter Replay |
| [Event Subscriptions](https://hermodr.deveel.org/subscriptions/) | Event dispatcher, filters, routing, and custom resolvers |
| [Event Schema](https://hermodr.deveel.org/schema/) | Schema definition, export (JSON / YAML / AsyncAPI), and validation |
| [Testing](https://hermodr.deveel.org/testing/) | Unit-testing event publishing |

## Future Work

The framework is still evolving. See the [ROADMAP](ROADMAP.md) for the full description of every planned feature and the version milestone in which each is expected to ship.

### v1.1 — Routing & Middleware ✅

- [x] **Event Subscription & Routing** — subscribe to event types with attribute-based filtering and in-process routing
- [x] **Event Middleware Pipeline** — composable cross-cutting hooks (logging, validation, correlation, tracing)

### v1.2 — Reliability

- [x] **Event Replay & Dead-Letter Handling** — capture and resubmit failed events with configurable retry and back-off
- [x] **Outbox Pattern Integration** — guaranteed exactly-once publishing via a transactional outbox channel
- [ ] **Event Scheduler & Deferred Publishing** — defer event publishing to a future point in time or after a delay

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
- [ ] **MassTransit Consumer Bridge** — expose Hermodr subscriptions as MassTransit consumers and vice versa

### v2.1 — Testing & DX

- [ ] **Expanded Testing Utilities** — fluent publish assertions (`AssertPublished`, `AssertNotPublished`), in-memory event bus, and consumer-side test helpers
- [ ] **Local Development Console Sink** — zero-configuration channel that pretty-prints CloudEvents to the console during local development, with automatic exclusion in non-development environments
- [ ] **.NET Aspire Integration** — surface publish channels as Aspire resources for dashboard visibility, automatic broker provisioning, and OTLP trace export out of the box
- [ ] **`dotnet event` CLI Extension** — `dotnet` global tool adding `dotnet event new`, `schema export`, `schema validate`, `schema diff`, and `channel add` sub-commands for event scaffolding and schema governance
- [ ] **Standalone `hermodr` CLI** — self-contained cross-platform executable and Docker image exposing the same command surface without requiring the .NET SDK, with GitHub Actions action and machine-readable SARIF output

### v2.2 — Subscription Management

- [ ] **Subscription Management Framework** — provider-agnostic `ISubscriptionStore` abstraction with in-memory default and runtime lifecycle operations
- [ ] **Relational Registry Provider (Entity Framework Core)** — persist subscriptions in SQL Server, PostgreSQL, or SQLite with bundled migrations
- [ ] **Document Registry Provider (MongoDB)** — persist subscriptions as MongoDB documents with real-time change-stream synchronisation
- [ ] **Subscription Management REST API** — secured minimal-API endpoint group with OpenAPI metadata and change-notification webhooks

### v2.3 — Framework Integrations

- [ ] **MediatR Integration** — bridge `[Event]`-annotated `INotification` types to the CloudEvents publish pipeline and route inbound CloudEvents back as MediatR notifications
- [ ] **Wolverine Integration** — emit CloudEvents as a Wolverine message side-effect and route inbound CloudEvents into the Wolverine runtime via `IMessageBus`
- [ ] **Brighter Integration** — publish CloudEvents as a post-handler pipeline step for Brighter `ICommand` / `IEvent` types and bridge inbound CloudEvents into the Brighter command processor

Monitor the [open issues](https://github.com/deveel/hermodr/issues) to see what is being actively worked on.

## Contributors

Thanks go to all the people who have contributed to this project!

[![Contributors](https://contrib.rocks/image?repo=deveel/hermodr)](https://github.com/deveel/hermodr/graphs/contributors)

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
