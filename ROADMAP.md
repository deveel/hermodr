# Deveel Events — Roadmap

This document outlines the planned evolution of the **Deveel Events** framework. Items are grouped by milestone and ordered by planned delivery. All proposals are subject to community feedback — open an issue or discussion if you want to influence prioritisation.

---

## Version Strategy & Milestones

The table below maps each roadmap item to the release milestone in which it is planned to ship. Version numbers follow [Semantic Versioning](https://semver.org/): a **minor** bump (`x.Y.0`) delivers new, backward-compatible capabilities; a **major** bump (`X.0.0`) signals a significant architectural expansion.

| Milestone | Version | Theme                                                                                                                                                                                                                                                                                                                                                                                                                                                                                 | Items |
|-----------|---------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|-------|
| **Stable Publisher** | **v1.0.0** | Harden the publishing and schema packages, freeze public APIs, and ship production-ready documentation.                                                                                                                                                                                                                                                                                                                                                                               | — (current publisher + schema feature set) |
| **Routing & Middleware** | **v1.1.0** | Introduce the foundational subscription & routing abstraction and the event middleware pipeline, enabling all later consumer-side and observability work.                                                                                                                                                                                                                                                                                                                             | 1 · 2 |
| **Reliability** | **v1.2.0** | Add dead-letter capture and replay, the outbox pattern, and the event scheduler to make publishing robust to transient failures and deferred delivery requirements.                                                                                                                                                                                                                                                                                                                   | 3 · 4 · 5 |
| **Observability** | **v1.3.0** | End-to-end distributed tracing via OpenTelemetry, schema validation at publish time, the append-only event store / audit log channel, and the publish delivery log for operational delivery telemetry.                                                                                                                                                                                                                                                                                | 6 · 7 · 8 · 9 |
| **Schema Governance** | **v1.4.0** | Formal schema versioning, compatibility checking, upcasting, and AsyncAPI / schema export tooling improvements.                                                                                                                                                                                                                                                                                                                                                                       | 10 · 11 |
| **New Transports** | **v1.5.0** | CloudEvents HTTP binding compliance for the Webhook publisher, a new lightweight HTTP CloudEvents channel, plus new channel adapters for gRPC streaming, Apache Kafka, Amazon SQS, Amazon SNS, Google Cloud Pub/Sub, and NATS/JetStream.                                                                                                                                                                                                                                              | 12 · 13 · 14 · 15 · 16 · 17 · 18 · 19 |
| **Event Consumers** | **v2.0.0** | First-class consumer adapters — ASP.NET Core Webhook framework, pre-built SaaS platform adapters (Facebook, SendGrid, Twilio, Stripe, GitHub, Shopify), RabbitMQ, Azure Service Bus, and MassTransit — completing the publish / consume lifecycle. This is a **major** release because it introduces a new, independently versioned surface area (`Deveel.Events.Consumer.*` packages) and changes the framing of the framework from a pure publisher to a full event-driven toolkit. | 20 · 21 · 22 · 23 · 24 |
| **Testing & DX** | **v2.1.0** | Expanded testing utilities with fluent publish assertions, an in-memory event bus, and consumer-side test helpers.                                                                                                                                                                                                                                                                                                                                                                    | 25 |
| **Subscription Management** | **v2.2.0** | Provider-agnostic subscription management framework, EF Core and MongoDB registry providers, and a secured REST management API with OpenAPI metadata and change-notification webhooks.                                                                                                                                                                                                                                                                                                | 26 · 27 · 28 · 29 |
| **Framework Integrations** | **v2.3.0** | Bridges between Deveel Events and the four major .NET in-process mediator / command-bus frameworks — MediatR, Wolverine, Brighter — so teams can emit CloudEvents as a natural side-effect of existing handler dispatch and route inbound CloudEvents back into each framework's handler pipeline.                                                                                                                                                                                    | 30 · 31 · 32 · 33 |

---

## Routing & Middleware

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

### 2. Event Middleware Pipeline

> *A composable pipeline applied before and after every publish, analogous to ASP.NET Core middleware.*

**The problem today:** Cross-cutting concerns — logging, enrichment, schema validation, correlation ID injection — have no standard hook point between `EventPublisher` and `IEventPublishChannel`. Each application wires these up ad hoc.

**What we will build:** An `IEventMiddleware` interface with a `next` delegate pattern, and a `UseMiddleware<T>()` extension on `EventPublisherBuilder`. Middleware instances run in registration order on every publish call and have access to the full `CloudEvent` and the resolved channel list.

**Benefits:**
- Centralises cross-cutting concerns in reusable, independently testable units.
- First-party middleware will be provided for logging, schema validation (see item 8), and tracing (see item 6), making them opt-in with a single line of configuration.
- Third parties can distribute middleware as NuGet packages without forking the core.
- Zero impact on existing code — the pipeline is an additive layer over the current publish path.

---

## Reliability

### 3. Event Replay & Dead-Letter Handling

> *Capture events that fail delivery, inspect them, and replay them without reprocessing the entire stream.*

**The problem today:** `EventPublisherOptions.ThrowOnErrors = false` suppresses exceptions but discards the information entirely. There is no facility to recover failed events.

**What we will build:** A dead-letter channel abstraction (`IDeadLetterChannel`) that captures failed events together with their exception, timestamp, and attempted delivery metadata. A separate replay API will allow operators or background jobs to resubmit captured events to the original channel (or a different one), with configurable retry policies and back-off strategies.

**Benefits:**
- Eliminates silent data loss when a downstream broker is temporarily unavailable.
- Gives operators a clear inspection point for diagnosing integration failures.
- Supports compliance scenarios where every emitted event must eventually be processed.
- Pluggable storage backends (in-memory for tests, database or blob storage in production).

---

### 4. Outbox Pattern Integration

> *Write events to a local database table first, then dispatch them asynchronously — guaranteeing exactly-once publishing even when the broker is down.*

**The problem today:** Publishing an event in the same business transaction as the database write that triggered it is a dual-write problem. If the broker call fails, the database change has already been committed and the event is lost. If the events are published first, an application crash can cause duplicate side effects.

**What we will build:** A `Deveel.Events.Publisher.Outbox` package providing an outbox channel that persists events to a relational store (EF Core by default, with an extensible provider model) inside the same `DbContext` transaction as the business operation. A hosted service polls the outbox table and forwards committed events to the real channel, marking them as dispatched.

**Benefits:**
- Guarantees that every committed business operation produces its corresponding events, even in the face of network failures or process crashes.
- Fully transparent to application code — replace any other channel with the outbox channel.
- Compatible with all existing channel adapters (RabbitMQ, Azure Service Bus, etc.).
- Supports idempotency markers to safely handle duplicate delivery on the consumer side.

---

### 5. Event Scheduler & Deferred Publishing

> *Schedule events to be published at a future point in time, or after a configurable delay.*

**The problem today:** There is no built-in way to defer an event. Applications that need deferred semantics must implement their own timer logic or use broker-specific features (Azure Service Bus scheduled messages, RabbitMQ TTL queues) directly.

**What we will build:** A scheduler abstraction (`IEventScheduler`) with `ScheduleAsync(CloudEvent, DateTimeOffset)` and `ScheduleAfterAsync(CloudEvent, TimeSpan)` signatures. The default implementation persists scheduled events to a store (reusing the outbox infrastructure) and dispatches them via a `BackgroundService` at the appropriate time. Broker-native scheduling will be used where available for higher precision.

**Benefits:**
- Decouples the business decision of *when* to notify from the infrastructure details of how to defer it.
- Useful for reminders, SLA escalations, delayed notifications, and time-based workflow steps.
- Leverages native broker scheduling (Azure Service Bus, RabbitMQ) when available for lower overhead.
- Cancellation support allows scheduled events to be withdrawn before they fire.

---

## Observability

### 6. OpenTelemetry & Distributed Tracing Integration

> *Propagate and extract W3C trace context as CloudEvents extensions, enabling end-to-end traces across service boundaries.*

**The problem today:** Events cross process boundaries but carry no trace context, making it impossible to correlate a published event with the originating request in a distributed trace.

**What we will build:** A `Deveel.Events.Publisher.OpenTelemetry` package that instruments `EventPublisher` with Activity spans, injects W3C `traceparent`/`tracestate` as CloudEvents extension attributes on publish, and extracts them on the subscription/consumer side to continue the trace.

**Benefits:**
- Publishers and consumers appear as linked spans in tools like Jaeger, Zipkin, or Azure Monitor.
- No code changes required in business logic — tracing is applied through a middleware registration.
- Complies with the [CloudEvents distributed tracing extension](https://github.com/cloudevents/spec/blob/main/cloudevents/extensions/distributed-tracing.md) specification.
- Dramatically reduces mean-time-to-diagnosis for latency and failure issues in event-driven systems.

---

### 7. Event Store & Audit Log Channel

> *An append-only channel that persists every published domain event for auditing, debugging, or read-model rebuilding.*

**The problem today:** Once an event is dispatched to a broker it is gone from the application's perspective. Reproducing what happened at a given point in time requires access to broker logs or custom instrumentation.

**What we will build:** A `Deveel.Events.Publisher.EventStore` channel that writes every event to an append-only store (database table or blob storage, with a provider abstraction). The store supports querying by `type`, `source`, `subject`, time range, and custom attributes, and exposes a streaming API for replaying stored events in chronological order.

> **Scope note:** The Event Store records *domain facts* — the event payload, metadata, and CloudEvents attributes — not the operational outcome of the delivery attempt itself. For tracking delivery attempts, retries, error codes, and latency, see item 9 (Publish Delivery Log) below.

**Benefits:**
- Provides a complete audit trail of all domain events without relying on broker retention policies.
- Enables event-sourcing-style read-model reconstruction by replaying the stored stream.
- Useful in regulated industries where an immutable record of domain facts is a compliance requirement.
- Can be combined with the dead-letter and replay features to correlate failures with their originating events.
- Shares its storage provider abstraction with the Publish Delivery Log (item 9), so a single backend configuration covers both concerns.

---

### 8. Schema Validation at Publish Time

> *Validate every event against its registered schema before dispatching it to a channel.*

**The problem today:** The `Deveel.Events.Schema` and `Deveel.Events.Publisher` packages are entirely separate. An event with missing required fields or out-of-range values is published without complaint, and the error only surfaces at consumer side — or not at all.

> **Already shipped (patch):** `EventPublisher` now enforces the four required CloudEvents envelope attributes (`id`, `source`, `type`, `specversion`) after enrichment and before channel dispatch, throwing `InvalidCloudEventException` if any are absent. This is a minimal, envelope-only guard. Full _payload_ validation — checking the `data` field against its declared schema — is the scope of this item and remains deferred.

**What we will build:** A schema validation middleware (see item 2) that looks up the schema for a CloudEvent's `type` from the registered `IEventSchemaFactory` and runs the existing `IEventSchemaValidator` before the event reaches any channel. Failed validation can be configured to throw, log, or route to the dead-letter channel.

**Benefits:**
- Shifts schema violations left to the publisher boundary, where they are cheapest to fix.
- Reduces integration bugs caused by structurally invalid events reaching consumers.
- Works transparently with events declared using the `[Event]` and `[EventProperty]` annotations — no extra schema authoring required.
- Provides immediate developer feedback during local testing.

---

### 9. Publish Delivery Log

> *Record the operational outcome of every event publish attempt — channel, timestamp, attempt count, latency, and error details — across pluggable storage backends.*

**The problem today:** There is no built-in way to answer questions like *"how many times did we attempt to send event X before it succeeded?"*, *"which channel is producing the most failures?"*, or *"what was the average delivery latency last week?"*. The dead-letter channel (item 3) captures only failed events for replay, and the Event Store (item 7) records domain facts, not infrastructure telemetry. Ops teams must rely on generic APM tooling or custom logging to reconstruct delivery history.

**What we will build:** A `Deveel.Events.Publisher.DeliveryLog` package providing:
- An `IPublishDeliveryLog` abstraction that receives a `DeliveryRecord` after every publish attempt (whether successful or not), containing: event ID, channel name, attempt number, UTC timestamp, outcome (`Succeeded` / `Failed` / `Retried`), HTTP/AMQP error code, exception message, and elapsed time.
- A middleware component (see item 2) that intercepts each publish call and writes a record before and after the channel invocation.
- Provider implementations for common storage backends: **relational database** (EF Core, supporting SQL Server, PostgreSQL, SQLite), **file system** (NDJSON rolling files), and **in-memory** (for tests and local development).
- A shared `Deveel.Events.Storage` provider abstraction that is also used by the Event Store (item 7), so applications that need both can configure a single backend.
- A lightweight query API to retrieve delivery records by event ID, channel, outcome, or time range.

**Benefits:**
- Gives operations teams full visibility into publishing health without relying on broker-specific dashboards.
- Makes retry storms and chronic failure patterns immediately visible in application-level logs.
- The pluggable provider model means no single storage technology is mandated — use SQLite locally, PostgreSQL in staging, and a managed database in production.
- Sharing the storage abstraction with the Event Store reduces configuration duplication and maintenance overhead.
- Complements OpenTelemetry tracing (item 6): the delivery log captures structured operational data even when a distributed tracing backend is not available.
- Useful for SLA reporting: query average latency and success rate per channel over any time window.

---

## Schema Governance

### 10. Event Versioning & Compatibility

> *Formal tooling for schema evolution: compatibility checking, upcasting, and version-aware routing.*

**The problem today:** `IVersionedElement` exists in the schema package but is not enforced by any runtime behaviour. Breaking schema changes can silently invalidate existing consumers.

**What we will build:** A compatibility checker (backward/forward/full) that compares two versions of an `EventSchema` and reports breaking changes. An upcasting pipeline will allow producers to register transformations from old schema versions to new ones, so that replayed or legacy events are automatically migrated before handling. Version-aware routing will let subscriptions optionally pin to a specific schema version range.

**Benefits:**
- Prevents accidental breaking changes from reaching consumers in production.
- Enables smooth schema migrations without coordinated consumer deployments.
- The compatibility checker can be integrated into CI pipelines as a quality gate.
- Upcasting makes the event store (item 7) future-proof — stored events remain usable even after schema changes.

---

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

## Transport & Channels

### 12. CloudEvents HTTP Binding Compliance for the Webhook Publisher

> *Bring the existing Webhook publisher into full CloudEvents HTTP binding specification compliance — structured and binary content modes, correct `Content-Type` and `ce-*` headers — without changing its delivery semantics.*

**The problem today:** `Deveel.Events.Publisher.Webhook` delivers events over HTTP, but sends a raw JSON payload rather than a CloudEvents-structured or binary-mode message. Receivers that expect the canonical CloudEvents envelope — correct `Content-Type: application/cloudevents+json` header in structured mode, or `ce-*` attribute headers in binary mode — must perform custom remapping before they can process the payload.

**What we will build:** Targeted enhancements to `Deveel.Events.Publisher.Webhook`, keeping all existing delivery concerns (HMAC signing, per-subscriber retry policies, secret rotation, delivery receipts) completely unchanged:
- **Structured content mode** (`Content-Type: application/cloudevents+json`): serialises the full CloudEvents envelope and data into a single JSON object per the spec.
- **Binary content mode**: event attributes emitted as `ce-*` HTTP headers; the `data` field alone forms the request body with its native `datacontenttype`.
- Per-endpoint content-mode selection (default: structured); existing webhook configurations that rely on the current raw-JSON behaviour are unaffected until explicitly opted in.

**Benefits:**
- Receivers compliant with the [CloudEvents HTTP binding specification](https://github.com/cloudevents/spec/blob/main/cloudevents/bindings/http-protocol-binding.md) — Azure Event Grid, Knative Eventing, self-hosted consumers — can receive events from the Webhook publisher without custom header/body remapping.
- Zero breaking change: only the wire format changes when a new content-mode option is set; all signing, retrying, and subscriber-management behaviour is preserved.
- Binary mode allows the event `data` to be any content type (e.g., `application/protobuf`) and lets attribute-based routing happen at the HTTP layer without deserialising the body.

> **Scope note:** This item covers only CloudEvents wire-format compliance for the Webhook publisher. Lightweight, direct HTTP delivery to known service endpoints — without subscriber management, HMAC signing, or retry complexity — is addressed separately in item 13 below.

---

### 13. HTTP Publisher Channel (CloudEvents HTTP Binding)

> *A lightweight channel for publishing CloudEvents directly to known HTTP endpoints using the CloudEvents structured or binary content modes — with no subscriber registry, no signing, and no delivery-tracking overhead.*

**The problem today:** `Deveel.Events.Publisher.Webhook` is purpose-built for webhook delivery: it manages subscriber registrations, HMAC signing, per-subscriber retry policies, and delivery receipts. This machinery is valuable for fan-out webhook scenarios but is unnecessary overhead when publishing events directly to a known, trusted service endpoint — such as an internal API gateway, a sidecar, a Function app trigger URL, or an edge service. Teams needing simple HTTP delivery today must either use the full webhook publisher (dragging in unneeded complexity) or call `HttpClient` manually outside the framework pipeline.

**What we will build:** A new, minimal `Deveel.Events.Publisher.Http` package implementing `IEventPublishChannel`:
- Delivers events to one or more statically-configured endpoint URLs using the CloudEvents HTTP binding (structured or binary content mode, per item 12).
- Uses `IHttpClientFactory` for connection pooling and `HttpClient` lifetime management; no subscriber registry, no signing infrastructure, no delivery-receipt model.
- Per-endpoint authentication configurable via standard `HttpClient` handlers: Bearer token, API key header, or a custom `DelegatingHandler` — nothing beyond what `IHttpClientFactory` already provides.
- Per-endpoint resilience via `Microsoft.Extensions.Http.Resilience` retry and circuit-breaker policies, configured through the standard `IHttpStandardResiliencePipelineBuilder`.
- Fan-out: a single `PublishAsync` call delivers to all registered endpoints concurrently, with independent resilience policies per endpoint.
- Full integration with the middleware pipeline (item 2) and the dead-letter channel (item 3).
- `AddHttpEventPublisherChannel()` DI registration extension.

**Benefits:**
- Right-sized for direct service-to-service delivery: no signing secrets to manage, no subscriber state to persist, no delivery-tracking tables.
- Statically-configured endpoints mean the full middleware and dead-letter pipeline applies — a guarantee the Webhook publisher cannot offer when endpoints are registered dynamically at runtime.
- Useful in serverless and edge scenarios where the destination is a known Function trigger URL, an API gateway webhook receiver, or a CloudEvents-native platform endpoint (Azure Event Grid custom topics, Knative, etc.).
- Clean separation of concerns: the Webhook publisher owns dynamic subscriber management and signed delivery; the HTTP channel owns lightweight, trusted, point-to-point CloudEvents transport.

---

### 14. gRPC Publisher Channel

> *Stream CloudEvents over gRPC for low-latency, bidirectional service-to-service delivery.*

**The problem today:** High-throughput or latency-sensitive services that already use gRPC for their inter-service communication have no CloudEvents-native gRPC publishing channel and must mix transport styles.

**What we will build:** A `Deveel.Events.Publisher.Grpc` package providing a gRPC-based publisher channel:
- A Protobuf service definition aligned with the [CloudEvents gRPC protocol binding](https://github.com/cloudevents/spec/blob/main/cloudevents/bindings/grpc-protocol-binding.md), using the `cloudevents.v1.CloudEvent` Protobuf message type.
- Unary RPC publish for single events and client-streaming RPC for batched or high-throughput scenarios.
- TLS, mTLS, and `CallCredentials`-based authentication through the standard `GrpcChannel` configuration.
- Full integration with the middleware pipeline (item 2) and the dead-letter channel (item 3).
- `AddGrpcEventPublisherChannel()` DI registration extension compatible with `Grpc.Net.Client` and `Grpc.AspNetCore`.

**Benefits:**
- Significantly lower per-message overhead compared to HTTP/JSON for high-frequency event streams.
- Bidirectional streaming enables back-pressure-aware publish flows between cooperating services.
- Conforms to the CloudEvents gRPC binding specification for cross-platform interoperability.
- Reuses existing gRPC infrastructure (service mesh, load balancer, authentication) already in place in gRPC-native stacks.

---

### 15. Apache Kafka Publisher Channel

> *Publish CloudEvents to Apache Kafka topics, with support for partitioning, headers, and exactly-once semantics.*

**The problem today:** Apache Kafka is one of the most widely deployed event-streaming platforms, yet there is no first-party Deveel Events channel adapter for it. Teams using Kafka must maintain a separate publish path alongside any other Deveel Events channels.

**What we will build:** A `Deveel.Events.Publisher.Kafka` package implementing `IEventPublishChannel` on top of `Confluent.Kafka`:
- CloudEvents attributes mapped to Kafka message headers following the [CloudEvents Kafka protocol binding](https://github.com/cloudevents/spec/blob/main/cloudevents/bindings/kafka-protocol-binding.md).
- Per-channel topic configuration with optional message key extraction from a CloudEvents attribute or a configurable key selector delegate (for partition affinity).
- Producer configuration surface covering compression, batch size, `acks`, and `linger.ms` — fully accessible via options without forking the package.
- Exactly-once delivery support via Kafka transactions, opt-in per channel.
- Schema Registry integration (Confluent Schema Registry) for Avro or Protobuf serialisation of the event `data` field, with automatic subject registration.
- `AddKafkaEventPublisherChannel()` DI registration extension.

**Benefits:**
- Brings the full Deveel Events pipeline — enrichment, schema validation, middleware, dead-letter — to Kafka-based architectures without giving up Kafka's ordering and retention guarantees.
- Partition key control allows producers to co-locate related events on the same partition, preserving per-aggregate ordering.
- Schema Registry integration aligns with existing Confluent Platform governance workflows.
- Exactly-once option satisfies compliance requirements for financial and transactional event streams.

---

### 16. Amazon SQS Publisher Channel

> *Publish CloudEvents to Amazon SQS queues, including FIFO queues with deduplication and message-group ordering.*

**The problem today:** Teams running on AWS have no first-party Deveel Events adapter for SQS and must duplicate their publishing logic outside the framework, losing middleware, schema validation, and dead-letter integration.

**What we will build:** A `Deveel.Events.Publisher.AmazonSqs` package wrapping the AWS SDK v3 `IAmazonSQS` client:
- CloudEvents attributes carried as SQS message attributes.
- Standard queue and FIFO queue support; FIFO mode exposes `MessageGroupId` (mapped from a configurable CloudEvents attribute) and `MessageDeduplicationId` (defaulting to the CloudEvents `id`).
- Batch publish via `SendMessageBatchAsync` for throughput optimisation, with automatic splitting at the SQS 10-message batch limit.
- Large-message support via Amazon SQS Extended Client / S3 offload for payloads exceeding 256 KB.
- IAM credential resolution through the standard AWS SDK credential chain (environment variables, instance profile, assumed role).
- `AddAmazonSqsEventPublisherChannel()` DI registration extension.

**Benefits:**
- Enables fully-managed, serverless event publishing on AWS without operating a broker.
- FIFO queue support provides per-entity ordering and exactly-once delivery guarantees at the SQS level.
- Batch publish reduces API calls and cost for high-volume event streams.
- Works seamlessly alongside the existing Azure Service Bus channel in multi-cloud architectures.

---

### 17. Amazon SNS Publisher Channel

> *Fan out CloudEvents to Amazon SNS topics for multi-subscriber delivery across SQS queues, Lambda functions, HTTP endpoints, and mobile push.*

**The problem today:** Amazon SNS is the standard AWS mechanism for pub/sub fan-out to heterogeneous subscribers, yet there is no first-party Deveel Events adapter, forcing teams to call the SNS SDK directly outside the framework pipeline.

**What we will build:** A `Deveel.Events.Publisher.AmazonSns` package wrapping the AWS SDK v3 `IAmazonSimpleNotificationService` client:
- CloudEvents attributes mapped to SNS message attributes for subscriber-side filtering.
- SNS message filtering policy integration: the channel can be configured to set attribute values that match subscriber filter policies, enabling content-based fan-out without custom routing code.
- FIFO SNS topic support with `MessageGroupId` and deduplication ID propagation (mirroring the SQS FIFO channel, item 16).
- Raw message delivery mode for SQS subscriber stacks that expect the CloudEvent JSON body directly without the SNS envelope wrapper.
- `AddAmazonSnsEventPublisherChannel()` DI registration extension.

**Benefits:**
- A single `PublishAsync` call fans the event out to all SNS subscribers — SQS queues, Lambda functions, HTTP/S endpoints, and email — without the publisher needing to know the subscriber topology.
- SNS message attribute filtering lets the broker perform content-based routing, reducing unnecessary message delivery to uninterested subscribers.
- Complements the SQS channel (item 16): SNS → SQS is the canonical AWS fanout-to-queue pattern and both channels share the same AWS SDK credential configuration.

---

### 18. Google Cloud Pub/Sub Publisher Channel

> *Publish CloudEvents to Google Cloud Pub/Sub topics with attribute-based filtering and ordering key support.*

**The problem today:** Teams running on Google Cloud have no first-party Deveel Events adapter for Pub/Sub and must publish events outside the framework, bypassing the middleware, schema validation, and dead-letter pipeline.

**What we will build:** A `Deveel.Events.Publisher.GooglePubSub` package wrapping the Google Cloud Pub/Sub client library (`Google.Cloud.PubSub.V1`):
- CloudEvents attributes mapped to Pub/Sub message attributes following the [CloudEvents Pub/Sub protocol binding](https://github.com/cloudevents/spec/blob/main/cloudevents/bindings/pubsub-protocol-binding.md).
- Ordering key support: a configurable selector maps a CloudEvents attribute (e.g., `subject` or a custom extension) to the Pub/Sub ordering key, enabling per-entity ordered delivery to subscribers with ordering enabled.
- Batching via the `PublisherClient` flow control settings, surfaced as framework channel options.
- Application Default Credentials (ADC) and Workload Identity support through the standard Google Auth library credential resolution chain.
- `AddGooglePubSubEventPublisherChannel()` DI registration extension.

**Benefits:**
- Brings the full Deveel Events pipeline to GCP-native workloads without a separate publish path.
- Ordering key integration provides per-aggregate event ordering — a key correctness requirement for event-sourced systems.
- ADC / Workload Identity credential resolution requires zero credential management code in application services.
- Complements the AWS channels (items 16–17) for teams operating multi-cloud or migrating between providers.

---

### 19. NATS / NATS JetStream Publisher Channel

> *Publish CloudEvents to NATS subjects or JetStream streams for ultra-low-latency, cloud-native messaging.*

**The problem today:** NATS is widely used in cloud-native and edge environments for its extremely low latency and simple operational model, but there is no first-party Deveel Events channel adapter for it.

**What we will build:** A `Deveel.Events.Publisher.Nats` package wrapping the `NATS.Net` client library:
- Core NATS mode: publishes events as NATS messages on a configurable subject derived from the CloudEvents `type` or a custom mapping function.
- JetStream mode: publishes to a named stream with optional per-message deduplication (using the CloudEvents `id` as the `Nats-Msg-Id` header) and publish acknowledgement awaiting.
- CloudEvents attributes carried as NATS message headers.
- TLS and NKey / JWT credential support through the standard NATS connection options.
- `AddNatsEventPublisherChannel()` DI registration extension with separate `UseNatsCore()` and `UseJetStream()` configuration sub-paths.

**Benefits:**
- Sub-millisecond publish latency for services where broker round-trip time is a performance constraint.
- JetStream persistence and deduplication support enables at-least-once delivery guarantees without a heavier broker.
- Lightweight operational footprint — NATS runs as a single binary with no external dependencies, making it ideal for edge, IoT, and sidecar deployments.
- The subject-per-type mapping convention integrates naturally with NATS subject hierarchies and wildcard subscriptions.

---

## Event Consumers

### 20. Webhook Consumer Framework for ASP.NET Core

> *A transport-agnostic foundation for receiving and dispatching inbound webhook events inside an ASP.NET Core application.*

**The problem today:** Deveel Events only covers the *publishing* side of the event lifecycle. Services that need to receive webhook payloads — for example, from a SaaS platform or another Deveel Events publisher — must build their own endpoint, deserialisation, signature verification, and routing logic from scratch.

**What we will build:** A `Deveel.Events.Consumer.Webhook` package providing:
- An ASP.NET Core middleware and a minimal-API endpoint registration (`MapCloudEventWebhook(...)`) that accepts HTTP POST requests carrying CloudEvents in structured or binary content mode.
- An `IWebhookSignatureVerifier` abstraction for pluggable signature verification, with a built-in HMAC-SHA256/384/512 implementation.
- Deserialisation of the payload into a typed `CloudEvent` and routing through the `IEventSubscription` registry (item 1).
- An `IWebhookPayloadMapper` extensibility point that translates arbitrary third-party JSON payloads into `CloudEvent` objects before dispatch — the foundation that service-specific adapters (item 21) build on.
- Appropriate HTTP status codes and problem-detail responses on validation or deserialization failure.

**Benefits:**
- Turns any ASP.NET Core application into a capable CloudEvents consumer with a single `UseCloudEventWebhook()` or `MapCloudEventWebhook()` call.
- Integrates with the existing subscription and routing infrastructure — no separate consumer-side wiring needed.
- The pluggable `IWebhookPayloadMapper` and `IWebhookSignatureVerifier` abstractions allow any third-party webhook format and signing scheme to be supported without forking the core package.
- Compatible with any platform that delivers events over HTTP webhooks (GitHub, Stripe, Azure Event Grid, etc.).

---

### 21. Pre-built Webhook Consumer Adapters

> *Ready-made adapters that translate the proprietary webhook payloads of major SaaS platforms into `CloudEvent` objects and verify their signatures automatically.*

**The problem today:** Even with the generic webhook consumer framework in place (item 20), teams integrating with popular SaaS platforms must still write the platform-specific payload mapping and signature verification themselves. Each platform uses a different JSON schema, a different signing mechanism (HMAC, RSA, Ed25519, shared token), and a different delivery header convention, resulting in repeated boilerplate across projects.

**What we will build:** A suite of thin adapter packages — each implementing `IWebhookPayloadMapper` and `IWebhookSignatureVerifier` from item 20 — for the most widely used webhook-emitting platforms:

| Package | Platform | Signature scheme |
|---------|----------|-----------------|
| `Deveel.Events.Consumer.Webhook.Facebook` | Meta / Facebook Graph API (Messenger, WhatsApp Business, Instagram) | HMAC-SHA256 (`X-Hub-Signature-256`) |
| `Deveel.Events.Consumer.Webhook.SendGrid` | SendGrid Event Webhook | HMAC-SHA256 (`X-Twilio-Email-Event-Webhook-Signature`) |
| `Deveel.Events.Consumer.Webhook.Twilio` | Twilio (SMS, Voice, WhatsApp) | HMAC-SHA1 (`X-Twilio-Signature`) |
| `Deveel.Events.Consumer.Webhook.Stripe` | Stripe (payments, subscriptions, disputes) | HMAC-SHA256 (`Stripe-Signature` with timestamp replay protection) |
| `Deveel.Events.Consumer.Webhook.GitHub` | GitHub (push, PR, release, issues) | HMAC-SHA256 (`X-Hub-Signature-256`) |
| `Deveel.Events.Consumer.Webhook.Shopify` | Shopify (orders, products, customers) | HMAC-SHA256 (`X-Shopify-Hmac-Sha256`) |

Each adapter package:
- Registers with `AddFacebookWebhookConsumer()` / `AddSendGridWebhookConsumer()` etc. via `IServiceCollection` extension methods, wiring the platform-specific mapper and verifier into the framework from item 20.
- Maps canonical platform event fields to standard CloudEvents attributes (`type`, `source`, `subject`, `id`, `time`) and preserves the original payload in `data`.
- Exposes strongly-typed event objects (e.g., `FacebookMessagingEvent`, `StripePaymentIntentEvent`) that consumers can work with after subscription routing.
- Includes a test helper (`FakeWebhookSender`) that generates correctly signed HTTP requests for use in unit and integration tests.

**Benefits:**
- Eliminates per-project boilerplate for the most common webhook integrations — drop in a package and register the adapter.
- Signature verification is handled correctly out of the box for each platform's specific scheme, reducing the risk of security mistakes (missing timestamp validation, incorrect HMAC algorithm, etc.).
- Strongly-typed event objects make handler code readable and refactor-safe.
- Test helpers make it straightforward to write deterministic, broker-free tests for webhook-triggered flows.
- New adapters for additional platforms can be contributed as standalone packages without touching the core framework.

---

### 22. RabbitMQ Consumer

> *Consume CloudEvents from RabbitMQ queues and exchanges and route them through the subscription registry.*

**The problem today:** `Deveel.Events.Publisher.RabbitMq` can publish events to RabbitMQ, but there is no companion consumer. Applications must hand-roll `IBasicConsumer` implementations, CloudEvents deserialization, and error handling separately.

**What we will build:** A `Deveel.Events.Consumer.RabbitMq` package providing a `BackgroundService`-based hosted consumer that:
- Declares queues and bindings from configuration or attributes.
- Deserialises incoming AMQP messages to `CloudEvent` objects.
- Routes them through the `IEventSubscription` registry (item 1).
- NAcks and optionally dead-letters messages that fail deserialization or handler dispatch.
- Supports prefetch limits, concurrent handler execution, and graceful shutdown.

**Benefits:**
- Pairs naturally with the existing RabbitMQ publisher to form a complete publish/subscribe solution.
- Leverages the shared subscription registry so the same handler registration code works regardless of transport.
- Dead-letter integration (item 3) provides automatic recovery and replay for failed messages.
- Configuration-driven queue and binding setup removes broker-specific boilerplate from application code.

---

### 23. Azure Service Bus Consumer

> *Consume CloudEvents from Azure Service Bus queues and topics/subscriptions and route them through the subscription registry.*

**The problem today:** `Deveel.Events.Publisher.AzureServiceBus` covers publishing but not consumption. Teams using Azure Service Bus must integrate the SDK's `ServiceBusProcessor` independently, including CloudEvents mapping and error policies.

**What we will build:** A `Deveel.Events.Consumer.AzureServiceBus` package wrapping `ServiceBusProcessor` in a `BackgroundService` that:
- Receives messages from configurable queues or topic subscriptions.
- Maps Azure Service Bus message properties to CloudEvents attributes.
- Routes deserialized events through the `IEventSubscription` registry (item 1).
- Handles dead-lettering, lock renewal, and session-aware processing.

**Benefits:**
- Completes the Azure Service Bus integration with a consistent publish/consume API.
- Takes advantage of native Service Bus features (sessions, deferred messages, scheduled delivery) through configuration.
- Uniform handler model means the same event handler works with RabbitMQ, Azure Service Bus, or any future consumer adapter.

---

### 24. MassTransit Consumer Bridge

> *Expose Deveel Events subscriptions as MassTransit consumers, and vice versa, to unify both programming models.*

**The problem today:** `Deveel.Events.Publisher.MassTransit` delegates publishing to MassTransit but does not expose a complementary consumer side. Teams using MassTransit for consumption must maintain two separate routing models.

**What we will build:** A `Deveel.Events.Consumer.MassTransit` package that registers `IEventSubscription` handlers as MassTransit `IConsumer<T>` implementations automatically, and optionally maps inbound MassTransit messages to `CloudEvent` objects before dispatching them through the registry.

**Benefits:**
- Projects already invested in MassTransit can adopt Deveel Events incrementally, starting with the consumer side, without replacing their existing topology.
- The shared handler model means a subscription declared once can be driven by MassTransit, RabbitMQ, or any other consumer adapter.
- Reduces duplication of consumer registration boilerplate in mixed-stack services.

---

## Developer Experience

### 25. Expanded Testing Utilities

> *First-class test helpers for asserting which events were published, with what attributes, and in what order.*

**The problem today:** `Deveel.Events.TestPublisher` provides a basic in-memory publisher, but there are no assertion helpers, no subscription testing support, and no way to assert negative cases (event was *not* published).

**What we will build:** A rich `EventPublisherAssertions` API (compatible with xUnit, NUnit, and MSTest) offering fluent assertions such as `AssertPublished<TEvent>()`, `AssertPublishedWith(e => e.Source == ...)`, `AssertPublishedInOrder(...)`, and `AssertNotPublished<TEvent>()`. An in-memory event bus will allow integration tests to exercise full publish-subscribe round trips without a real broker.

**Benefits:**
- Makes event-driven behaviour a first-class testable concern alongside the domain model.
- Fluent assertion API dramatically reduces test boilerplate.
- The in-memory bus enables full-stack integration tests that run in milliseconds, without Docker or a real broker.
- Negative assertions catch regressions where a previously emitted event is accidentally removed.

---

## Subscription Management

### 26. Subscription Management Framework

> *A provider-agnostic framework for persisting, querying, and lifecycle-managing event subscriptions at runtime.*

**The problem today:** The subscription registry introduced in item 1 is entirely in-memory and must be re-populated from code on every application start. There is no facility to create, update, suspend, or remove subscriptions at runtime without redeploying the application, and no standard model for sharing subscription state across multiple service instances.

**What we will build:** A `Deveel.Events.Subscriptions.Management` package providing the core framework layer:
- An `EventSubscription` entity model enriched with lifecycle state (`Active`, `Suspended`, `Deleted`), ownership metadata (tenant ID, created-by, timestamps), and a version counter for optimistic concurrency.
- An `ISubscriptionStore` abstraction — the single extension point that storage providers implement — with methods for `CreateAsync`, `UpdateAsync`, `DeleteAsync`, `FindByIdAsync`, `ListAsync`, and a `WatchAsync` streaming overload for change notifications.
- An `ISubscriptionRegistry` service built on top of `ISubscriptionStore`, adding higher-level operations: `EnableAsync`, `DisableAsync`, `TransferOwnershipAsync`, and bulk registration from configuration or assembly scanning.
- An `ISubscriptionSyncService` abstraction that bridges the durable store and the in-process routing table from item 1: implementations receive change notifications from the store and apply them to the live dispatcher without a restart.
- A default **in-memory** `ISubscriptionStore` for local development and testing.
- DI registration extensions (`AddSubscriptionManagement()`, `UseInMemorySubscriptionStore()`) integrated with the standard `IServiceCollection` pattern.

**Benefits:**
- Establishes a clean separation between the management framework and its storage backends, keeping the core package free of infrastructure dependencies.
- Subscriptions become first-class, lifecycle-managed entities rather than static code registrations.
- The in-memory default means teams can adopt the framework immediately in tests and development before choosing a persistence backend.
- `ISubscriptionSyncService` makes cluster-aware hot-reload of routing changes possible without any restart, regardless of which storage provider is used.

---

### 27. Relational Registry Provider (Entity Framework Core)

> *Persist subscription state in any EF Core-compatible relational database — SQL Server, PostgreSQL, or SQLite — with ready-made migrations and polling-based synchronisation.*

**The problem today:** Teams running on relational databases have no first-party way to back the subscription registry with an existing database infrastructure.

**What we will build:** A `Deveel.Events.Subscriptions.EntityFramework` package implementing `ISubscriptionStore` on top of EF Core:
- A `SubscriptionDbContext` with full `EventSubscription` entity configuration (column mappings, indexes on `type`, `source`, `tenantId`, and `state`).
- Bundled EF Core migrations targeting SQL Server, PostgreSQL, and SQLite; migration auto-apply option for non-production scenarios.
- A polling-based `ISubscriptionSyncService` implementation that queries for changes since a stored high-water-mark timestamp and feeds them to the in-process routing table.
- `UseEntityFrameworkSubscriptionStore<TContext>()` DI registration extension that works with any existing `DbContext` or the dedicated `SubscriptionDbContext`.

**Benefits:**
- Zero additional infrastructure for teams already using a relational database — the subscription table lives alongside their domain tables.
- EF Core's provider abstraction means the same package targets SQL Server, PostgreSQL, SQLite, and any other EF Core-supported engine without code changes.
- Bundled migrations eliminate hand-crafted DDL scripts and keep the schema in sync with code automatically.
- Polling-based sync is operationally simple and works with any relational engine, including those that do not support change-data-capture.

---

### 28. Document Registry Provider (MongoDB)

> *Persist subscription state as MongoDB documents with change-stream-based synchronisation for real-time, cluster-wide routing updates.*

**The problem today:** Teams running on MongoDB have no first-party way to back the subscription registry without writing a custom `ISubscriptionStore` implementation from scratch.

**What we will build:** A `Deveel.Events.Subscriptions.MongoDb` package implementing `ISubscriptionStore` on top of the official MongoDB .NET driver:
- A document model for `EventSubscription` with BSON serialisation attributes and compound indexes on `type`, `source`, `tenantId`, and `state` for fast filtered queries.
- A change-stream-based `ISubscriptionSyncService` implementation that opens a resume-token-aware change stream on the subscriptions collection and pushes inserts, updates, and deletes to the in-process routing table in real time — with automatic resume after network interruption.
- `UseMongoDbSubscriptionStore()` DI registration extension accepting a connection string, `IMongoDatabase`, or an `IMongoClient` + database name pair.
- Support for multi-tenant collection isolation (one collection per tenant or a shared collection with a tenant discriminator field), configurable at service-registration time.

**Benefits:**
- Change-stream-based synchronisation propagates routing changes to all cluster nodes in milliseconds, with no polling overhead.
- Resume tokens survive process restarts: no subscription change is missed even if the service is temporarily offline.
- MongoDB's flexible document model naturally accommodates the open-ended extension attributes carried by `EventSubscription` without schema migrations.
- Pairs well with existing MongoDB-native services that already use the same cluster for domain data.

---

### 29. Subscription Management REST API

> *Expose subscription lifecycle operations as secured HTTP endpoints, providing an admin surface for operations tooling and self-service tenant portals.*

**The problem today:** Even with a durable subscription registry in place, there is no standardised HTTP interface for creating, inspecting, or modifying subscriptions from external tooling, CI pipelines, or tenant administration dashboards. Each team must build its own controller layer on top of `ISubscriptionRegistry`.

**What we will build:** A `Deveel.Events.Subscriptions.Management.Api` package adding a minimal-API endpoint group to any ASP.NET Core application:
- `MapSubscriptionManagementApi(prefix)` registers a self-contained endpoint group (default prefix `/subscriptions`) with the following endpoints:
  - `GET /subscriptions` — paginated list with filtering by `type`, `source`, `state`, and `tenantId`.
  - `GET /subscriptions/{id}` — retrieve a single subscription by ID.
  - `POST /subscriptions` — create a new subscription; validates filter expressions and event type against the registered schema (if available).
  - `PUT /subscriptions/{id}` — replace a subscription's filter and routing configuration.
  - `PATCH /subscriptions/{id}/state` — enable or suspend a subscription without replacing its full configuration.
  - `DELETE /subscriptions/{id}` — soft-delete with an optional hard-delete flag.
- Full OpenAPI / Swagger metadata generated via `Microsoft.AspNetCore.OpenApi` annotations, compatible with Scalar and Swashbuckle.
- Authorization policy hooks: each endpoint group can be secured independently using standard ASP.NET Core `IAuthorizationPolicy` names passed at registration time (`MapSubscriptionManagementApi(o => o.RequirePolicy("SubscriptionAdmin"))`).
- An `ETag`-based optimistic-concurrency model on `PUT` and `PATCH` using the `EventSubscription` version counter from item 26.
- Optional Webhook callback on subscription changes: posts a CloudEvent payload to a configured URL whenever a subscription is created, updated, or deleted — enabling external systems to react to registry mutations without polling.

**Benefits:**
- Provides an immediately usable admin surface without requiring each consuming team to write their own controller layer.
- The secured, policy-driven endpoint model makes it safe to expose the API to tenant administrators in multi-tenant deployments.
- OpenAPI metadata makes the management API discoverable and self-documenting for operations tooling and internal developer portals.
- Soft-delete and state transitions support audit and compliance workflows that prohibit hard deletion of subscription records.
- The optional change-notification webhook closes the loop for event-driven infrastructure automation that reacts to subscription lifecycle events.

---

## Framework Integrations

### 30. MediatR Integration

> *Bridge MediatR notifications to the Deveel Events publishing pipeline, and optionally surface incoming CloudEvents as MediatR notifications — so MediatR-native applications can adopt CloudEvents without restructuring existing handler code.*

**The problem today:** Applications that already use MediatR as their in-process messaging bus must maintain two separate dispatch paths: one for MediatR `INotification` / `IRequest` handling and one for Deveel Events CloudEvent publishing. There is no standard hook that automatically intercepts a dispatched MediatR notification and routes it through the Deveel Events enrichment, middleware, and channel pipeline, nor any facility to surface an inbound CloudEvent as a MediatR notification so that existing `INotificationHandler<T>` implementations can react to it without change.

**What we will build:** A `Deveel.Events.Publisher.MediatR` package providing:
- A MediatR `INotificationHandler<TNotification>` base implementation — and an opt-in `IPipelineBehavior<TRequest, TResponse>` for request-side interception — that detects notifications annotated with `[Event]` (or implementing a configurable marker interface) and forwards them to `IEventPublisher` as CloudEvents, applying the full enrichment, middleware, and channel dispatch pipeline.
- A `CloudEventNotificationMapper` that translates between `INotification` and `CloudEvent`, deriving the CloudEvents `type`, `source`, and `subject` from annotation metadata (compatible with the existing `[Event]` and `[EventProperty]` attribute model) or from a configurable mapping delegate.
- An inbound bridge: an `IEventSubscription` handler (item 1) that receives CloudEvents from the subscription registry and re-publishes them as MediatR `INotification` objects through the `IMediator` pipeline, so that existing `INotificationHandler<T>` implementations react to external events with no code changes.
- `AddMediatREventPublisher()` and `AddMediatREventConsumer()` DI registration extensions, fully compatible with the standard `MediatR.Extensions.Microsoft.DependencyInjection` / `MediatR` v12+ registration pattern.
- A test helper that captures CloudEvents emitted by MediatR notifications, compatible with the `Deveel.Events.TestPublisher` infrastructure.

**Benefits:**
- MediatR-native applications can adopt Deveel Events incrementally: annotate an existing `INotification` and it begins flowing through the full CloudEvents pipeline — enrichment, schema validation, middleware, dead-letter — without restructuring any handler or command code.
- The pipeline-behavior and notification-handler interception points mean cross-cutting concerns (logging, tracing, schema validation) are applied consistently whether the CloudEvent originates from a MediatR notification or from a direct `IEventPublisher` call.
- The inbound bridge allows consumer-side handlers to remain written as standard `INotificationHandler<T>`, fully independent of the broker transport that delivered the event (RabbitMQ, Azure Service Bus, webhook, etc.).
- No lock-in: the integration is purely additive — removing the package leaves both the MediatR handler tree and the Deveel Events pipeline fully functional and independently operable.
- Works seamlessly with the middleware pipeline (item 2), schema validation (item 8), OpenTelemetry tracing (item 6), and the dead-letter channel (item 3), inheriting all cross-cutting capabilities with zero additional configuration.

### 31. Wolverine Integration

> *Expose Deveel Events publishing as a Wolverine message side-effect, and route inbound CloudEvents through the Wolverine runtime — so Wolverine-native applications gain CloudEvents interoperability without leaving their existing handler model.*

**The problem today:** Wolverine (JasperFx) has become a popular dual-mode messaging framework: it handles both in-process command/event dispatch and out-of-process transport (RabbitMQ, Azure Service Bus, Amazon SQS) through a single `IMessageBus`. Applications built on Wolverine have no standard way to emit CloudEvents from a Wolverine message handler or to receive CloudEvents and route them into the Wolverine runtime for handler discovery and execution.

**What we will build:** A `Deveel.Events.Publisher.Wolverine` package providing:
- A Wolverine `IMessageMiddleware` (or side-effect policy) that intercepts outgoing messages annotated with `[Event]` and publishes them through `IEventPublisher` as CloudEvents, running the full enrichment, middleware, and channel pipeline.
- A `CloudEventWolverineHandler` base that receives CloudEvents from the Deveel Events subscription registry (item 1) and re-dispatches them into the Wolverine runtime via `IMessageBus.PublishAsync`, enabling existing Wolverine handlers to react to externally sourced CloudEvents.
- A `CloudEventMessageMapper` that maps between Wolverine `Envelope` metadata (correlation ID, conversation ID, tenant ID) and the equivalent CloudEvents extension attributes.
- `AddWolverineEventPublisher()` and `AddWolverineEventConsumer()` DI registration extensions compatible with Wolverine's `WolverineOptions` fluent configuration API.

**Benefits:**
- Wolverine applications can emit standards-compliant CloudEvents to any Deveel Events channel (RabbitMQ, Azure Service Bus, Kafka, HTTP) without bypassing the existing Wolverine handler model or Wolverine's built-in transactional outbox.
- The `Envelope` ↔ CloudEvent attribute mapping preserves Wolverine's native correlation and tenant context across service boundaries.
- Inbound routing through the Wolverine runtime means CloudEvent-triggered flows benefit from Wolverine's retry, error-handling, and local-queue features — no duplicate infrastructure required.
- Works side-by-side with Wolverine's own transport integrations: teams can use Wolverine for internal messaging and Deveel Events CloudEvents channels for external, spec-compliant event publishing.

---

### 32. Brighter Integration

> *Integrate Deveel Events into the Paramore Brighter command-processor pipeline — publish CloudEvents as a side-effect of dispatched commands, and route inbound CloudEvents as Brighter `IEvent` objects.*

**The problem today:** Paramore Brighter (`Paramore.Brighter`) implements the Command Processor pattern with `IAmACommandProcessor` offering `Send`, `Publish`, and `Post` semantics. Applications built around Brighter have no standard way to emit a CloudEvent as a side-effect of a dispatched command or event, nor any facility to bridge inbound CloudEvents into the Brighter handler pipeline.

**What we will build:** A `Deveel.Events.Publisher.Brighter` package providing:
- A Brighter `IHandleRequests<T>` pipeline step (using Brighter's `IAmAPipelineStep<T>` attribute-driven decorator model) that intercepts dispatched `ICommand` and `IEvent` types annotated with `[Event]` and publishes them through `IEventPublisher` as CloudEvents after the primary handler succeeds.
- A `CloudEventBrighterMapper` that derives CloudEvents `type`, `source`, `subject`, and `id` from the Brighter message's `Id`, `Header`, and annotation metadata.
- An inbound bridge: an `IAmAMessageMapper<CloudEvent>` implementation and a companion `IHandleRequestsAsync<CloudEventMessage>` handler that accept CloudEvents delivered by the Deveel Events subscription registry (item 1) and dispatch them into the Brighter runtime via `IAmACommandProcessor.PublishAsync`.
- `AddBrighterEventPublisher()` and `AddBrighterEventConsumer()` DI registration extensions compatible with `ServiceCollectionExtensions.AddBrighter()`.
- Optional support for Brighter's built-in outbox (`IAmAnOutbox<T>`) as an alternative to the Deveel Events Outbox channel (item 4), surfaced as a configuration option rather than a forced dependency.

**Benefits:**
- Command-processor teams can adopt CloudEvents as their external event contract incrementally: decorate an existing `IEvent` with `[Event]` and it begins flowing to the configured channels with no handler changes.
- Brighter's pipeline-step model ensures CloudEvent publishing is transactionally safe: the step only fires after the primary handler succeeds, preventing phantom events from failed operations.
- The inbound bridge preserves Brighter's strongly-typed handler discovery — a CloudEvent received from any transport arrives as a concrete `IEvent` type, selected by the `type` attribute.
- Optional outbox integration avoids duplicating outbox infrastructure when a team is already using Brighter's own outbox support.

---

### Version increment rationale

| Bump | Trigger |
|------|---------|
| **Patch** (`x.y.Z`) | Bug fixes, documentation corrections, dependency updates with no API changes. |
| **Minor** (`x.Y.0`) | New packages or APIs added in a backward-compatible way; no changes to existing public interfaces. |
| **Major** (`X.0.0`) | Architectural expansion that introduces a fundamentally new surface area (e.g., consumer packages in v2.0), or any breaking change to existing public APIs. |

Pre-release labels (`-alpha`, `-beta`, `-rc`) will be used on feature branches and release-candidate branches in accordance with the existing GitVersion configuration.

---

## Tracking Progress

Items will be tracked as GitHub milestones and issues. To propose a new feature, adjust the priority of an existing one, or contribute an implementation, please open an issue or start a discussion in the repository.