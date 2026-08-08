# Frameworks Comparison

Hermodr does not try to compete with other messaging frameworks in the .NET ecosystem: these frameworks solve different problems and can be complementary in the same architecture. The table below is a quick positioning guide, not a ranking.

| Icon | Meaning |
| :---: | :--- |
| ✅ | Full native support |
| ⚠️ | Available but not a standardised or first-class feature |
| ❌ | Not natively provided |

| Feature | `Hermodr` | `MassTransit` | `Wolverine` | `NServiceBus` | `Rebus` |
| :--- | :---: | :---: | :---: | :---: | :---: |
| CloudEvents‑first contract model | ✅ | ❌ | ❌ | ❌ | ❌ |
| Event metadata annotations | ✅ | ❌ | ❌ | ❌ | ❌ |
| Schema export (JSON/YAML/AsyncAPI) | ✅ | ❌ | ❌ | ❌ | ❌ |
| AsyncAPI document generation | ✅ | ❌ | ❌ | ❌ | ❌ |
| Multi‑transport adapters | ✅ | ✅ | ✅ | ✅ | ✅ |
| Transactional outbox | ✅ | ✅ | ✅ | ✅ | ✅ |
| Dead‑letter + replay | ✅ | ⚠️ | ⚠️ | ⚠️ | ⚠️ |
| Deferred / scheduled delivery | ⚠️ | ✅ | ✅ | ✅ | ✅ |
| In‑process subscription routing | ✅ | ✅ | ✅ | ✅ | ✅ |
| Middleware / extensibility pipeline | ✅ | ✅ | ✅ | ✅ | ✅ |
| Testing utilities | ✅ | ✅ | ✅ | ✅ | ✅ |

Capabilities evolve quickly across all projects, so validate details against each framework's current documentation.

## General Overview

### Hermodr

Hermodr is purpose-built for **CloudEvents-native event publishing**. Every event in the pipeline is modelled as a `CloudEvent` — from creation through enrichment, middleware processing, channel dispatch, and audit/logging — making the framework a natural fit for teams that want to adopt the CloudEvents standard as their integration contract.

The framework focuses on the **publish side** of the event lifecycle: it provides a transport-agnostic pipeline, rich schema governance (JSON Schema, YAML, AsyncAPI exports), and reliability extensions (outbox, dead-letter replay, delivery log) out of the box. Consumer-side features and subscription management are being introduced in subsequent releases.

Hermodr is a lightweight, middleware-oriented framework. It does not prescribe aggregate design, saga orchestration, or CQRS — it slots into any DDD or clean-architecture codebase as the event egress layer.

**Ideal for:** Teams that want CloudEvents as their standard event contract, need schema exports and AsyncAPI governance, and prefer a focused publish pipeline over a full service bus.

### MassTransit

MassTransit is a mature, feature-rich service bus for .NET. It provides a comprehensive set of abstractions for message-based communication, including publish/subscribe, request/response, sagas, routing slips, and state machines.

MassTransit supports multiple transports (RabbitMQ, Azure Service Bus, Amazon SQS, Kafka, ActiveMQ) and integrates deeply with dependency injection containers. Its saga support is production-proven and widely adopted.

MassTransit was not designed with CloudEvents as the native message format — it uses its own message contract model. CloudEvents can be used as a contract by convention, but there is no first-class CloudEvents pipeline, schema export, or annotation system. The Hermodr.MassTransit channel bridges the two frameworks, allowing Hermodr to publish CloudEvents through a MassTransit-backed transport.

**Ideal for:** Teams that need saga orchestration, state machines, or a full-featured service bus with multi-transport support and do not require CloudEvents-native contracts.

### Wolverine

Wolverine is a command- and message-dispatch framework built on the .NET middleware pipeline. It emphasises low-ceremony development: handlers are plain classes with conventional method signatures, and the framework handles routing, retries, and error policies automatically.

Wolverine integrates closely with EF Core and Marten for transactional outbox and inbox patterns, and supports RabbitMQ, Azure Service Bus, Amazon SQS, and TCP transports. It also provides local command buses for in-process mediation.

Wolverine uses its own message contract model rather than CloudEvents. There is no schema generation or AsyncAPI export. Its middleware pipeline is handler-oriented rather than publish-oriented, which makes it better suited for command dispatch than pure event broadcasting.

**Ideal for:** Teams that want a low-ceremony command/event dispatch framework with strong database integration and are not tied to CloudEvents.

### NServiceBus

NServiceBus is an enterprise-grade service bus with a commercial licensing model. It provides advanced messaging patterns including sagas, claim-check (data bus), deferred delivery, and publish/subscribe with persistent subscription storage.

NServiceBus has deep integration with the Microsoft ecosystem, particularly SQL Server, Azure, and the .NET DI container. It offers a mature operational tooling surface including monitoring, performance counters, and a management portal (ServicePulse).

NServiceBus uses its own message contract model and does not natively expose CloudEvents. The framework is opinionated about handler structure, unit-of-work boundaries, and transport configuration, which provides consistency at the cost of flexibility. Licensing costs should be factored into the evaluation.

**Ideal for:** Enterprise environments already invested in the Microsoft ecosystem that need a supported, feature-complete service bus with saga orchestration, and are willing to adopt NServiceBus's message contract model.

### Rebus

Rebus is a lightweight, unobtrusive service bus for .NET. It positions itself as a simple, easy-to-understand alternative to the more heavyweight frameworks, with a focus on minimal ceremony and straightforward configuration.

Rebus supports multiple transports (Azure Service Bus, RabbitMQ, Amazon SQS, SQL Server via `Rebus.ServiceProvider`) and provides publish/subscribe, sagas, timeouts (deferred delivery), and handler pipelines. It is fully open-source with no paid licensing.

Rebus does not natively support CloudEvents or schema export. Its extensibility model is straightforward — handlers are plain classes and the pipeline is configured through `RebusConfigurer` — but there is no built-in system for event metadata annotations, AsyncAPI governance, or publish middleware in the Hermodr sense.

**Ideal for:** Teams that want a simple, unopinionated service bus without licensing costs, and are comfortable implementing CloudEvents contracts at the application layer.

## When to choose what

| Scenario | Recommended framework |
|----------|----------------------|
| CloudEvents-native event contracts, schema governance, AsyncAPI exports | **Hermodr** |
| Saga orchestration and state machines | **MassTransit** or **NServiceBus** |
| Lightweight command dispatch with database integration | **Wolverine** |
| Enterprise-supported service bus in the Microsoft ecosystem | **NServiceBus** |
| Simple, unopinionated, zero-cost service bus | **Rebus** |
| Publish CloudEvents through an existing MassTransit transport | **Hermodr** (via the `Hermodr.Publisher.MassTransit` channel) |

These frameworks are not mutually exclusive. Hermodr can coexist alongside any of them — for example, use MassTransit for saga workflows and Hermodr for CloudEvents-native event broadcasting, with the Hermodr.MassTransit channel connecting the two.

---

← Back to [Documentation Home](README.md)