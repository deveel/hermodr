# Deveel Events — Roadmap

This document outlines the planned evolution of the **Deveel Events** framework. Items are grouped by theme and ordered roughly by priority, though many can progress in parallel. All proposals are subject to community feedback — open an issue or discussion if you want to influence prioritisation.

---

## Reliability

### 1. Event Subscription & Routing

> *Subscribe to an event type, optionally with attribute-based filtering, and have it automatically routed to the designated channel.*

**The problem today:** Deveel Events is a pure publishing framework. Consumers must implement their own demultiplexing logic on top of the raw channel primitives, leading to repeated boilerplate.

**What we will build:** An `IEventSubscription` abstraction and a companion subscription registry. Subscribers declare which CloudEvents `type` (and optionally `source`, `subject`, or arbitrary extension attributes) they are interested in. An in-process dispatcher matches incoming events against the registry and invokes the appropriate handler on the correct channel. Filtering will support exact matches, prefix wildcards, and predicate delegates for advanced cases.

**Benefits:**
- Eliminates hand-rolled routing code in every consuming service.
- Makes the event flow explicit and auditable in one place.
- Pairs naturally with the existing schema and annotation packages — filter expressions can reference schema-declared property names.
- Provides a foundation for all other consumer-side features below.

---

### 2. Event Replay & Dead-Letter Handling

> *Capture events that fail delivery, inspect them, and replay them without reprocessing the entire stream.*

**The problem today:** `EventPublisherOptions.ThrowOnErrors = false` suppresses exceptions but discards the information entirely. There is no facility to recover failed events.

**What we will build:** A dead-letter channel abstraction (`IDeadLetterChannel`) that captures failed events together with their exception, timestamp, and attempted delivery metadata. A separate replay API will allow operators or background jobs to resubmit captured events to the original channel (or a different one), with configurable retry policies and back-off strategies.

**Benefits:**
- Eliminates silent data loss when a downstream broker is temporarily unavailable.
- Gives operators a clear inspection point for diagnosing integration failures.
- Supports compliance scenarios where every emitted event must eventually be processed.
- Pluggable storage backends (in-memory for tests, database or blob storage in production).

---

### 3. Outbox Pattern Integration

> *Write events to a local database table first, then dispatch them asynchronously — guaranteeing exactly-once publishing even when the broker is down.*

**The problem today:** Publishing an event in the same business transaction as the database write that triggered it is a dual-write problem. If the broker call fails, the database change has already been committed and the event is lost. If the events are published first, an application crash can cause duplicate side effects.

**What we will build:** A `Deveel.Events.Publisher.Outbox` package providing an outbox channel that persists events to a relational store (EF Core by default, with an extensible provider model) inside the same `DbContext` transaction as the business operation. A hosted service polls the outbox table and forwards committed events to the real channel, marking them as dispatched.

**Benefits:**
- Guarantees that every committed business operation produces its corresponding events, even in the face of network failures or process crashes.
- Fully transparent to application code — replace any other channel with the outbox channel.
- Compatible with all existing channel adapters (RabbitMQ, Azure Service Bus, etc.).
- Supports idempotency markers to safely handle duplicate delivery on the consumer side.

---

## Observability

### 4. Event Middleware Pipeline

> *A composable pipeline applied before and after every publish, analogous to ASP.NET Core middleware.*

**The problem today:** Cross-cutting concerns — logging, enrichment, schema validation, correlation ID injection — have no standard hook point between `IEventPublisher` and `IEventPublishChannel`. Each application wires these up ad hoc.

**What we will build:** An `IEventMiddleware` interface with a `next` delegate pattern, and a `UseMiddleware<T>()` extension on `EventPublisherBuilder`. Middleware instances run in registration order on every publish call and have access to the full `CloudEvent` and the resolved channel list.

**Benefits:**
- Centralises cross-cutting concerns in reusable, independently testable units.
- First-party middleware will be provided for logging, schema validation (see item 5), and tracing (see item 6), making them opt-in with a single line of configuration.
- Third parties can distribute middleware as NuGet packages without forking the core.
- Zero impact on existing code — the pipeline is an additive layer over the current publish path.

---

### 5. OpenTelemetry & Distributed Tracing Integration

> *Propagate and extract W3C trace context as CloudEvents extensions, enabling end-to-end traces across service boundaries.*

**The problem today:** Events cross process boundaries but carry no trace context, making it impossible to correlate a published event with the originating request in a distributed trace.

**What we will build:** A `Deveel.Events.Publisher.OpenTelemetry` package that instruments `IEventPublisher` with Activity spans, injects W3C `traceparent`/`tracestate` as CloudEvents extension attributes on publish, and extracts them on the subscription/consumer side to continue the trace.

**Benefits:**
- Publishers and consumers appear as linked spans in tools like Jaeger, Zipkin, or Azure Monitor.
- No code changes required in business logic — tracing is applied through a middleware registration.
- Complies with the [CloudEvents distributed tracing extension](https://github.com/cloudevents/spec/blob/main/cloudevents/extensions/distributed-tracing.md) specification.
- Dramatically reduces mean-time-to-diagnosis for latency and failure issues in event-driven systems.

---

### 6. Event Store & Audit Log Channel

> *An append-only channel that persists every published event for auditing, debugging, or read-model rebuilding.*

**The problem today:** Once an event is dispatched to a broker it is gone from the application's perspective. Reproducing what happened at a given point in time requires access to broker logs or custom instrumentation.

**What we will build:** A `Deveel.Events.Publisher.EventStore` channel that writes every event to an append-only store (database table or blob storage, with a provider abstraction). The store supports querying by `type`, `source`, `subject`, time range, and custom attributes, and exposes a streaming API for replaying stored events in chronological order.

**Benefits:**
- Provides a complete audit trail of all domain events without relying on broker retention policies.
- Enables event-sourcing-style read-model reconstruction by replaying the stored stream.
- Useful in regulated industries where an immutable record of domain facts is a compliance requirement.
- Can be combined with the dead-letter and replay features to correlate failures with their originating events.

---

## Schema Governance

### 7. Schema Validation at Publish Time

> *Validate every event against its registered schema before dispatching it to a channel.*

**The problem today:** The `Deveel.Events.Schema` and `Deveel.Events.Publisher` packages are entirely separate. An event with missing required fields or out-of-range values is published without complaint, and the error only surfaces at consumer side — or not at all.

**What we will build:** A schema validation middleware (see item 4) that looks up the schema for a CloudEvent's `type` from the registered `IEventSchemaFactory` and runs the existing `IEventSchemaValidator` before the event reaches any channel. Failed validation can be configured to throw, log, or route to the dead-letter channel.

**Benefits:**
- Shifts schema violations left to the publisher boundary, where they are cheapest to fix.
- Reduces integration bugs caused by structurally invalid events reaching consumers.
- Works transparently with events declared using the `[Event]` and `[EventProperty]` annotations — no extra schema authoring required.
- Provides immediate developer feedback during local testing.

---

### 8. Event Versioning & Compatibility

> *Formal tooling for schema evolution: compatibility checking, upcasting, and version-aware routing.*

**The problem today:** `IVersionedElement` exists in the schema package but is not enforced by any runtime behaviour. Breaking schema changes can silently invalidate existing consumers.

**What we will build:** A compatibility checker (backward/forward/full) that compares two versions of an `EventSchema` and reports breaking changes. An upcasting pipeline will allow producers to register transformations from old schema versions to new ones, so that replayed or legacy events are automatically migrated before handling. Version-aware routing will let subscriptions optionally pin to a specific schema version range.

**Benefits:**
- Prevents accidental breaking changes from reaching consumers in production.
- Enables smooth schema migrations without coordinated consumer deployments.
- The compatibility checker can be integrated into CI pipelines as a quality gate.
- Upcasting makes the event store (item 6) future-proof — stored events remain usable even after schema changes.

---

## Transport & Channels

### 9. HTTP & gRPC Channels

> *Deliver events directly over HTTP (CloudEvents HTTP binding) or gRPC, without requiring a message broker.*

**The problem today:** All existing channel adapters target message brokers (RabbitMQ, Azure Service Bus, MassTransit). Direct service-to-service or fan-out delivery over HTTP or gRPC requires custom code.

**What we will build:** A `Deveel.Events.Publisher.Http` package using `IHttpClientFactory` to POST CloudEvents to one or more configurable endpoints using the structured or binary content mode defined by the CloudEvents HTTP binding specification. A `Deveel.Events.Publisher.Grpc` package will follow using gRPC streaming. Both will integrate with the middleware pipeline and the dead-letter channel.

**Benefits:**
- Enables event publishing in environments where a message broker is unavailable or overkill (e.g., small services, serverless functions).
- The HTTP channel is an alternative to the existing Webhook channel, but with schema-aware content negotiation and CloudEvents headers.
- gRPC streaming enables low-latency, bidirectional event flows between services.
- Uniform API — application code does not change when switching between HTTP and broker channels.

---

### 10. Event Scheduler & Deferred Publishing

> *Schedule events to be published at a future point in time, or after a configurable delay.*

**The problem today:** There is no built-in way to defer an event. Applications that need deferred semantics must implement their own timer logic or use broker-specific features (Azure Service Bus scheduled messages, RabbitMQ TTL queues) directly.

**What we will build:** A scheduler abstraction (`IEventScheduler`) with `ScheduleAsync(CloudEvent, DateTimeOffset)` and `ScheduleAfterAsync(CloudEvent, TimeSpan)` signatures. The default implementation persists scheduled events to a store (reusing the outbox infrastructure) and dispatches them via a `BackgroundService` at the appropriate time. Broker-native scheduling will be used where available for higher precision.

**Benefits:**
- Decouples the business decision of *when* to notify from the infrastructure details of how to defer it.
- Useful for reminders, SLA escalations, delayed notifications, and time-based workflow steps.
- Leverages native broker scheduling (Azure Service Bus, RabbitMQ) when available for lower overhead.
- Cancellation support allows scheduled events to be withdrawn before they fire.

---

## Developer Experience

### 11. AsyncAPI & Schema Export Improvements

> *Auto-discover event types from assemblies and enrich the exported AsyncAPI document with server, channel, and operation definitions.*

**The problem today:** The `Deveel.Events.Schema.AsyncApi` package exports schemas but requires manual registration of each event type. Server URLs, channel bindings, and operation definitions are not populated automatically.

**What we will build:** Assembly scanning to auto-register all types annotated with `[Event]`, automatic population of AsyncAPI channel and operation objects from the registered publish channels (RabbitMQ exchange name, Azure Service Bus topic, etc.), and export as OpenAPI 3.1 webhooks in addition to AsyncAPI 2.x. A dotnet global tool will allow export as a CI step without a running application.

**Benefits:**
- No manual schema registration — annotate the class and it appears in the exported document automatically.
- The exported document becomes a living, always-up-to-date contract that can be published to a developer portal.
- OpenAPI 3.1 webhook export makes the schema accessible to REST-centric tooling.
- The CLI tool enables contract-first workflows and automated breaking-change detection in CI.

---

### 12. Expanded Testing Utilities

> *First-class test helpers for asserting which events were published, with what attributes, and in what order.*

**The problem today:** `Deveel.Events.TestPublisher` provides a basic in-memory publisher, but there are no assertion helpers, no subscription testing support, and no way to assert negative cases (event was *not* published).

**What we will build:** A rich `EventPublisherAssertions` API (compatible with xUnit, NUnit, and MSTest) offering fluent assertions such as `AssertPublished<TEvent>()`, `AssertPublishedWith(e => e.Source == ...)`, `AssertPublishedInOrder(...)`, and `AssertNotPublished<TEvent>()`. An in-memory event bus will allow integration tests to exercise full publish-subscribe round trips without a real broker.

**Benefits:**
- Makes event-driven behaviour a first-class testable concern alongside the domain model.
- Fluent assertion API dramatically reduces test boilerplate.
- The in-memory bus enables full-stack integration tests that run in milliseconds, without Docker or a real broker.
- Negative assertions catch regressions where a previously emitted event is accidentally removed.

---

## Tracking Progress

Items will be tracked as GitHub milestones and issues. To propose a new feature, adjust the priority of an existing one, or contribute an implementation, please open an issue or start a discussion in the repository.

