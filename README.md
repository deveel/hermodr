![GitHub License](https://img.shields.io/github/license/deveel/deveel.events)
![GitHub Release](https://img.shields.io/github/v/release/deveel/deveel.events) ![GitHub Actions Workflow Status](https://img.shields.io/github/actions/workflow/status/deveel/deveel.events/cicd.yml?logo=github)
![Codecov](https://img.shields.io/codecov/c/github/deveel/deveel.events?logo=codecov)

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

## Packages

| Package | Description | NuGet |
|---------|-------------|-------|
| `Deveel.Events.Annotations` | Attributes for describing event metadata on data classes | [![NuGet](https://img.shields.io/nuget/v/Deveel.Events.Annotations.svg)](https://www.nuget.org/packages/Deveel.Events.Annotations) |
| `Deveel.Events.Publisher` | Core publisher infrastructure (`IEventPublisher`, DI helpers) | [![NuGet](https://img.shields.io/nuget/v/Deveel.Events.Publisher.svg)](https://www.nuget.org/packages/Deveel.Events.Publisher) |
| `Deveel.Events.Publisher.AzureServiceBus` | Publish events to Azure Service Bus | [![NuGet](https://img.shields.io/nuget/v/Deveel.Events.Publisher.AzureServiceBus.svg)](https://www.nuget.org/packages/Deveel.Events.Publisher.AzureServiceBus) |
| `Deveel.Events.Amqp.Annotations` | AMQP-specific routing attributes (exchange, routing key) | [![NuGet](https://img.shields.io/nuget/v/Deveel.Events.Amqp.Annotations.svg)](https://www.nuget.org/packages/Deveel.Events.Amqp.Annotations) |
| `Deveel.Events.Publisher.RabbitMq` | Publish events to a RabbitMQ exchange | [![NuGet](https://img.shields.io/nuget/v/Deveel.Events.Publisher.RabbitMq.svg)](https://www.nuget.org/packages/Deveel.Events.Publisher.RabbitMq) |
| `Deveel.Events.Publisher.MassTransit` | Publish events through a MassTransit bus | [![NuGet](https://img.shields.io/nuget/v/Deveel.Events.Publisher.MassTransit.svg)](https://www.nuget.org/packages/Deveel.Events.Publisher.MassTransit) |
| `Deveel.Events.Publisher.Webhook` | Deliver events to HTTP webhook endpoints | [![NuGet](https://img.shields.io/nuget/v/Deveel.Events.Publisher.Webhook.svg)](https://www.nuget.org/packages/Deveel.Events.Publisher.Webhook) |
| `Deveel.Events.Schema` | Schema model, fluent builder, JSON writer, and validation | [![NuGet](https://img.shields.io/nuget/v/Deveel.Events.Schema.svg)](https://www.nuget.org/packages/Deveel.Events.Schema) |
| `Deveel.Events.Schema.Yaml` | Export an event schema as YAML | [![NuGet](https://img.shields.io/nuget/v/Deveel.Events.Schema.Yaml.svg)](https://www.nuget.org/packages/Deveel.Events.Schema.Yaml) |
| `Deveel.Events.Schema.AsyncApi` | Export schemas as an AsyncAPI 2.x document | [![NuGet](https://img.shields.io/nuget/v/Deveel.Events.Schema.AsyncApi.svg)](https://www.nuget.org/packages/Deveel.Events.Schema.AsyncApi) |

## Documentation

Full documentation — installation, quick-start, concept guides, channel references, schema export, and testing — is available in the [`docs/`](docs/README.md) folder of this repository.

| Section | Description |
|---------|-------------|
| [Getting Started](docs/getting-started/installation.md) | Installation and quick-start guide |
| [Core Concepts](docs/concepts/README.md) | Publisher, channels, and event annotations |
| [Publisher Channels](docs/publishers/README.md) | Azure Service Bus, RabbitMQ, MassTransit, Webhook |
| [Event Schema](docs/schema/README.md) | Schema definition, export (JSON / YAML / AsyncAPI), and validation |
| [Testing](docs/testing/README.md) | Unit-testing event publishing |

## Future Work

The framework is still evolving. Among the areas we plan to address:

- Supporting custom event serialisers and deserialisers
- Consumer-side support (deserialising events from channels)
- Named-channel selection at publish time

Monitor the [open issues](https://github.com/deveel/deveel.events/issues) to see what is being worked on.

## Contributing

We welcome bug reports, feature requests, and pull requests. Please read the [Contributing Guidelines](docs/contributing.md) before submitting.

## License

Released under the [MIT License](LICENSE).

## Authors

Developed and maintained by the [Deveel](https://deveel.com) team and released as an open-source project.
