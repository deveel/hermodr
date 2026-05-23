> **Renamed:** This project was formerly called **Deveel Events**. As of **23 May 2026**, it has been renamed to **Hermodr** — the messenger of the gods in Norse mythology — to reflect its role as a message delivery framework and distinguish it from the broader Deveel ecosystem.

# Hermodr

**Hermodr** is a lightweight, extensible framework for publishing domain events in .NET applications, built on top of the [CloudEvents](https://cloudevents.io/) standard.

## Domain Events in DDD

[Domain-Driven Design (DDD)](https://martinfowler.com/bliki/DomainDrivenDesign.html) treats **domain events** as first-class citizens of the model. A domain event represents something that _happened_ inside the domain — something meaningful to domain experts and worth recording.

> _"A domain event is a full-fledged part of the domain model, a representation of something that happened in the domain."_
> — Eric Evans, _Domain-Driven Design Reference_

### Why domain events matter

| Property | Meaning |
|----------|---------|
| **Fact-based** | Events describe what _happened_, not what _should happen_. They are immutable after they occur. |
| **Named in the ubiquitous language** | Event names (`OrderPlaced`, `InvoiceIssued`, `UserRegistered`) come directly from conversations with domain experts. |
| **Loosely coupled** | Producers and consumers are decoupled: the producing bounded context does not need to know who is listening. |
| **Bounded-context integration** | Events are the preferred mechanism for sharing information across bounded contexts without creating tight dependencies. |
| **Temporal decoupling** | Consumers can process events asynchronously, at their own pace, enabling reliable and scalable integrations. |

### Events vs. commands vs. queries

| Concept | Intent | Direction | Example |
|---------|--------|-----------|---------|
| **Command** | Request an action | One sender → one receiver | `PlaceOrder` |
| **Query** | Ask for data | One sender → one receiver | `GetOrderById` |
| **Event** | Notify that something happened | One producer → many consumers | `OrderPlaced` |

Events differ from commands in a subtle but important way: a command _could_ be rejected; an event is a statement of fact about the past — it already happened.

### Where Hermodr fits in

Hermodr implements the **publishing side** of domain events.  The framework is intentionally scoped: it does not dictate how you model your aggregates, store events, or rebuild read models.  What it does provide is a consistent, transport-agnostic way to broadcast domain events once they occur inside a bounded context.

```
Aggregate root raises event
        │
        ▼
  EventPublisher.PublishAsync(eventData)
        │
        ├──► Azure Service Bus queue
        ├──► RabbitMQ exchange
        ├──► Webhook endpoint
        └──► (any IEventPublishChannel)
```

## Hermodr vs Other .NET Messaging Frameworks

If your primary goal is to publish domain events as explicit integration contracts, framework scope matters more than feature count. Some frameworks are optimized for full service-bus runtime concerns (handler orchestration, endpoints, distributed workflows), while Hermodr is optimized for contract-first event publication around CloudEvents and schema governance.

This comparison focuses on what is natively provided by each framework core and packages in their standard ecosystem. It is not a ranking: teams often combine these tools depending on their architecture.

| Feature | `Hermodr` | `MassTransit` | `Wolverine` | `NServiceBus` | `Rebus` |
|---------|------------------|---------------|--------------|---------------|-----------|
| Event contract model | CloudEvents-first publish pipeline | Framework-native message contracts; CloudEvents not natively provided by the framework core | Framework-native message contracts; CloudEvents not natively provided by the framework core | Framework-native message contracts; CloudEvents not natively provided by the framework core | Framework-native message contracts; CloudEvents not natively provided by the framework core |
| Event metadata annotations | Built-in attributes (`Hermodr.Annotations`, AMQP extensions) | Event metadata annotations not natively provided by the framework core | Event metadata annotations not natively provided by the framework core | Event metadata annotations not natively provided by the framework core | Event metadata annotations not natively provided by the framework core |
| Schema export formats | JSON Schema, YAML, AsyncAPI packages | Schema export not natively provided by the framework core | Schema export not natively provided by the framework core | Schema export not natively provided by the framework core | Schema export not natively provided by the framework core |
| AsyncAPI generation | Dedicated package (`Hermodr.Schema.AsyncApi`) | AsyncAPI generation not natively provided by the framework core | AsyncAPI generation not natively provided by the framework core | AsyncAPI generation not natively provided by the framework core | AsyncAPI generation not natively provided by the framework core |
| Transport adapters included | Azure Service Bus, RabbitMQ, MassTransit, Webhook, Outbox, Dead-Letter | Native multi-transport broker integrations | Native multi-transport messaging endpoints | Native transport support via transport packages | Native transport integrations |
| Transactional outbox support | Built-in channel + EF integration packages | Natively supported | Natively supported | Natively supported | Natively supported |
| Dead-letter capture and replay | Dedicated dead-letter packages + replay worker model | Dead-letter handling available; replay workflow not natively standardized by the framework core | Dead-letter handling available; replay workflow not natively standardized by the framework core | Dead-letter handling available; replay workflow not natively standardized by the framework core | Dead-letter handling available; replay workflow not natively standardized by the framework core |
| Deferred/scheduled delivery | Planned (`Event Scheduler & Deferred Publishing` on roadmap) | Natively supported (transport/scheduler dependent) | Natively supported (runtime/transport dependent) | Natively supported (transport dependent) | Natively supported (transport dependent) |
| In-process subscription routing | Built-in subscriptions package (`Hermodr.Subscriptions`) | Native consumer/handler pipeline | Native local and remote handlers | Native message handler pipeline | Native message handler pipeline |
| Middleware/extensibility pipeline | Built-in event middleware pipeline | Native filters/middleware/observers | Native middleware and handler pipeline extensions | Native pipeline behaviors and extensibility points | Native pipeline steps and extensibility points |
| Testing support for publish flow | Dedicated in-memory test publisher package | Native test harness support | Native testing utilities | Native testing support | Native testing support |

Why teams choose Hermodr over these frameworks usually comes down to contract ownership and boundary clarity:

- They want a **CloudEvents-native model** instead of treating CloudEvents as an adapter concern.
- They need **schema artifacts as first-class outputs** (JSON Schema, YAML, AsyncAPI) to version and review alongside code.
- They want a **thin publishing layer** that does not force a full service-bus programming model into every bounded context.
- They need **transport flexibility** while keeping one event contract and one publisher abstraction.
- They want to add reliability patterns (outbox, dead-letter replay) without coupling the domain model to a single broker runtime.

Choose Hermodr when the hardest problem in your system is maintaining stable event contracts across teams and over time. Choose a full messaging runtime when your hardest problem is orchestrating complex consumer workflows and endpoint-level operational behavior. In many systems, a practical approach is combining both: Hermodr for contract-first publication at domain boundaries, and a broker/runtime framework for downstream processing topology.

Framework capabilities evolve; verify current details in each framework's official documentation before making a final decision.

## Event Schemas and Async API Contracts

Publishing an event is only half the story.  Consumers need to know the **shape** of the event — which properties it carries, their types, and which constraints apply — so they can deserialise it correctly and build reliable integrations.

This is where **event schemas** play the same role for asynchronous messaging that OpenAPI/Swagger plays for synchronous REST APIs.

### The problem without schemas

- Consumers guess the payload structure from code examples or tribal knowledge.
- A producer renames a field; consumers break silently.
- There is no machine-readable contract to validate against or generate client code from.

### Event schemas as async API contracts

An event schema documents the contract between a producer and its consumers:

```
Producer                                     Consumer(s)
───────                                      ─────────────
[Event("order.placed", "1.0")]               Reads schema
public class OrderPlacedData                 Validates payload
{                                            Generates typed client
    [Required] public Guid OrderId { get; set; }
    [Required] public decimal Amount { get; set; }
    [Required] public string Currency { get; set; }
    public string? Notes { get; set; }
}

         ──► EventSchema.FromDataType<OrderPlacedData>()
                      │
                      ├──► JSON Schema
                      ├──► YAML schema document
                      └──► AsyncAPI 2.x document
```

The `Hermodr.Schema` package can derive a schema automatically from annotated data classes, or you can build one explicitly with the fluent `EventSchemaBuilder`.  Either way, the schema can then be:

- **Exported as JSON** — for integration with schema registries or tooling.
- **Exported as YAML** — for human-readable documentation or version-controlled contracts.
- **Exported as an AsyncAPI document** — a complete, machine-readable API specification for asynchronous messaging, analogous to an OpenAPI document for REST.  AsyncAPI tooling can generate documentation sites, client SDKs, and mock servers from it.
- **Used for validation** — the `IEventSchemaValidator` service can validate a `CloudEvent` instance against the schema before it is published, preventing malformed events from reaching consumers.

### Schema versioning and stability

Treat your event schemas the same way you treat public API contracts:

- **Version them** using the `version` property (e.g. `"1.0"`, `"2.0"`).
- **Prefer additive changes** — adding a new nullable property is backward-compatible; removing or renaming a required property is breaking.
- **Communicate breaking changes** by incrementing the major version and publishing the new schema separately, giving consumers time to migrate.

## What the Framework Provides

- A **unified `EventPublisher` service** that fans out events to one or more registered channels.
- **Channel implementations** for Azure Service Bus, RabbitMQ, MassTransit, HTTP Webhooks, and a transactional outbox — each installable as a separate NuGet package.
- **In-process subscriptions and routing** via `AddSubscriptions()`, filters, and resolver extensibility.
- **Channel implementations** for Azure Service Bus, RabbitMQ, MassTransit, and HTTP Webhooks — each installable as a separate NuGet package.
- **Reliability extensions** for dead-letter handling, replay, and the transactional outbox.
- **Annotation attributes** (`[Event]`, `[EventProperty]`) to describe event metadata directly on your data classes, in the ubiquitous language of the domain.
- **Schema support** — derive, build, and export event schemas to JSON, YAML, and AsyncAPI documents.
- **Validation** — validate `CloudEvent` instances against a schema before publishing.
- A **test channel** to make unit-testing event publishing straightforward.

## Design Philosophy

The framework intentionally does **not** aim to be a full event-sourcing or message-broker solution.  Its goal is a thin, opinionated layer that lets every team publish domain events in a consistent way without rewriting the same plumbing every time.

> If you need durable event storage, complex routing, or consumer-side processing at scale, consider pairing this library with a dedicated message broker (RabbitMQ, Kafka, Azure Service Bus) — Hermodr already ships channel adapters for the most popular ones.

## CloudEvents Standard

All events are modelled as [`CloudEvent`](https://github.com/cloudevents/spec) objects, ensuring maximum interoperability with cloud platforms and services that implement the CNCF CloudEvents specification.

## Next Steps

| Topic | Description |
|-------|-------------|
| [Installation](getting-started/installation.md) | How to install the packages |
| [Quick Start](getting-started/quick-start.md) | Publish your first event in minutes |
| [Core Concepts](concepts/README.md) | Understand the building blocks |
| [Publisher Channels](publishers/README.md) | Configure a specific transport |
| [Event Subscriptions](subscriptions/README.md) | In-process dispatch, filters, routing, and resolvers |
| [Publisher Channels](publishers/README.md) | Configure a specific transport |
| [Publisher Channels](publishers/README.md) | Configure transports plus reliability features such as outbox and dead-letter replay |
| [Event Schema](schema/README.md) | Schema definition, export, and validation |
| [Testing](testing/README.md) | Unit-test event publishing |
| [Samples](samples/README.md) | Runnable end-to-end example projects |

## License

Released under the [MIT License](https://github.com/deveel/hermodr/blob/main/LICENSE).  
Developed and maintained by the [Deveel](https://deveel.com) team.
