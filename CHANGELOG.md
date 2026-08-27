# Changelog

## [Unreleased]

### 🚀 Features

- **New `Hermodr.Publisher.Grpc` package** — a gRPC-based publish channel
  (roadmap item 14) that delivers CloudEvents to statically-configured gRPC
  endpoints using [grpc-dotnet](https://github.com/grpc/grpc-dotnet)
  (`Grpc.Net.Client`) for the low-level HTTP/2 client:
  - `AddGrpcEventPublisherChannel()` registration extensions, configured via an
    `Action` delegate or an `IConfiguration` section; the typed variants
    (`AddGrpcEventPublisherChannel<TEvent>()`) route only events of a given data
    class and support per-event-type overrides merged over the general channel
    defaults.
  - `GrpcPublishOptions` / `GrpcPublishOptions<TEvent>` with a static
    `Endpoints` collection; each `GrpcEndpoint` carries its own `Address`,
    `HttpClientName`, `SenderName`, `Deadline`, and `Headers`.
  - Every publish call **fans the event out concurrently** to all configured
    endpoints, each resolved through `IHttpClientFactory` (wrapped as a
    `GrpcChannel` via `GrpcChannelOptions.HttpClient`) with its own TLS/mTLS
    and `CallCredentials` configuration.
  - **Pluggable `IGrpcEventSender` strategy** — the channel delegates the
    actual gRPC RPC (unary for single events, client-streaming for batches) to
    a user-supplied sender that calls their `.proto`-generated gRPC client.
    A `GrpcCallContext` carries the `CallInvoker`, per-endpoint headers,
    deadline, and cancellation token. A `.proto` generator for `[Event]`-
    annotated types (producing both the `.proto` contract and a generated
    `IGrpcEventSender` automatically) is planned for v1.6.0.
  - Per-endpoint sender selection via `GrpcEndpoint.SenderName` and named
    `AddGrpcEventSender<T>(name)` registration.
  - `IBatchEventPublishChannel` support — batch publishes use a
    client-streaming RPC per endpoint.
  - TLS, mTLS, and `CallCredentials` configured through the standard
    `IHttpClientFactory` / `SocketsHttpHandler` surface via
    `ConfigureGrpcChannel(name, configure)` — no bespoke gRPC configuration API.
  - Transport failures and non-OK gRPC statuses surface as
    `GrpcPublishException` subclasses (`GrpcTransportException`), so the
    existing error-handling pipeline (dead-letter, delivery log, tracing)
    engages automatically.
  - OpenTelemetry publish span per endpoint (`transport.publish.grpc`),
    consistent with the other transport channels.
  - New `EventTransports.Grpc` transport identifier and `GrpcPublishOptions`
    channel metadata (`address`, `address.N`), exposed for the AsyncAPI /
    OpenAPI exporters.

## v1.5.2 - HTTP Publisher Channel

### 🚀 Features

- **New `Hermodr.Publisher.Http` package** — a lightweight, first-party publish
  channel (roadmap item 13) that delivers CloudEvents to statically-configured
  HTTP endpoints using the
  [CloudEvents HTTP binding](https://github.com/cloudevents/spec/blob/main/cloudevents/bindings/http-protocol-binding.md)
  (structured or binary content mode) — with no subscriber registry, no signing,
  and no delivery-receipt overhead, complementing `Hermodr.Publisher.Webhook`:
  - `AddHttpEventPublisherChannel()` registration extensions, configured via an
    `Action` delegate or an `IConfiguration` section; the typed variants
    (`AddHttpEventPublisherChannel<TEvent>()`) route only events of a given data
    class and support per-event-type overrides merged over the general channel
    defaults.
  - `HttpPublishOptions` / `HttpPublishOptions<TEvent>` with a static
    `Endpoints` collection; each `HttpEndpoint` carries its own `Format`,
    `HttpClientName`, `Authentication`, `Resilience`, `RequestTimeout`, and
    `AdditionalHeaders`.
  - Every publish call **fans the event out concurrently** to all configured
    endpoints, each resolved through `IHttpClientFactory` with its own named
    client (and therefore its own authentication handlers and standard
    resilience pipeline).
  - Per-endpoint wire format: structured content modes (`cloudevents+json`,
    `cloudevents+xml`) embed the whole CloudEvent envelope in the body, while
    the binary content mode (`cloudevents+binary`) sends the raw event `data`
    as the body (with its native `datacontenttype` as `Content-Type`) and the
    context attributes as `ce-*` HTTP headers.
  - Built-in authentication via `HttpEndpointAuthentication` (`None`, `Bearer`,
    `ApiKey` with configurable header name) applied through `DelegatingHandler`s
    registered in DI; custom handlers, TLS, and timeouts attach to any endpoint's
    named client via `ConfigureHttpClient`.
  - Per-endpoint resilience on top of `AddStandardResilienceHandler()`:
    retry (attempts, exponential delay, jitter) and circuit-breaker
    (sampling duration, minimum throughput, failure ratio, break duration)
    configurable through the bindable `HttpResilienceOptions`; `429/500/502/503/504`
    are retryable by default.
  - Transport failures and non-success status codes surface as
    `HttpPublishException` subclasses (`HttpTransportException`,
    `HttpStatusCodeException`), so the existing error-handling pipeline
    (dead-letter, delivery log, tracing) engages automatically.
  - OpenTelemetry publish span per endpoint, consistent with the other transport
    channels.
  - New `EventTransports.Http` transport identifier and `HttpPublishOptions`
    channel metadata (`url`, `url.N`), exposed for the AsyncAPI / OpenAPI
    exporters.

- **CI fix**: stage the `dotnet test --coverage` reports (named `<guid>.cobertura.xml`)
  into `coverage-reports/` as `coverage-*.cobertura.xml` so the Codecov upload
  finds real coverage data on every OS.

### ⚠ Breaking Changes

- **None** — the new HTTP channel is additive; all existing channel behaviour is
  unchanged.

### 📖 Documentation

- New docs page: [HTTP Channel](docs/publishers/http.md), package listing in
  `packages.md`, and Docusaurus version snapshot `v1.5.2`.

**Full Changelog**: https://github.com/deveel/hermodr/compare/v1.5.1...v1.5.2

## v1.5.1 - Dapr Publisher Channel

### 🚀 Features

- **New `Hermodr.Publisher.Dapr` package** — a first-party publish channel
  (roadmap item 44) that delivers CloudEvents through the [Dapr pub/sub building
  block](https://docs.dapr.io/developing-applications/building-blocks/pubsub/pubsub-overview/)
  on top of `DaprClient`, keeping the underlying broker (Kafka, RabbitMQ, Azure
  Service Bus, NATS, ...) a Dapr component-configuration concern:
  - `AddDapr()` / `AddDapr<TEvent>()` registration extensions, configured via an
    `Action` delegate or an `IConfiguration` section; the typed variant supports
    per-event-type overrides merged over the general channel defaults.
  - `DaprPublishOptions` / `DaprPublishOptions<TEvent>` — `PubSubName`, `Topic`,
    `TopicSelector`, `MapAttributesToMetadata`, `MetadataPrefix` (default
    `"ce-"`), `DaprClientName`, and `ScheduleDeliveryAt`.
  - Topic resolution order: explicit `Topic` → `TopicSelector` delegate →
    CloudEvent `subject` attribute.
  - The CloudEvent is serialised in **structured mode** (canonical CloudEvents
    JSON envelope) and published verbatim, so consumers receive the canonical
    envelope regardless of the underlying broker; CloudEvent attributes are
    additionally projected onto Dapr metadata with a configurable prefix.
  - `IDaprClientBuilder` + default `DaprClientBuilder` resolve named (keyed)
    `DaprClient` instances, enabling mTLS / token-credential configuration of
    multiple sidecars via the standard Dapr SDK surface.
  - Transport failures are wrapped in `DaprPublishException`, so the existing
    error-handling pipeline (dead-letter, delivery log, tracing) engages
    automatically.
  - OpenTelemetry: publish span with `messaging.dapr.pubsubname` and destination
    tags, consistent with the other transport channels.
  - New `EventTransports.Dapr` transport identifier, exposed through channel
    metadata for the AsyncAPI / OpenAPI exporters.

- **CI fix**: drop the `files` glob from the Codecov upload step (the v5 action
  expanded it into stray CLI arguments).

### ⚠ Breaking Changes

- **None** — the new Dapr channel is additive; all existing channel behaviour is
  unchanged.

### 📖 Documentation

- New docs page: [Dapr Publisher Channel](docs/publishers/dapr.md), package
  listing in `packages.md`, and Docusaurus version snapshot `v1.5.1`.

**Full Changelog**: https://github.com/deveel/hermodr/compare/v1.5.0...v1.5.1

## v1.5.0 - CloudEvents HTTP Binding Compliance for the Webhook Publisher

### 🚀 Features

- **CloudEvents binary HTTP content mode for the Webhook publisher** (roadmap item 12):
  - New `EventMessageFormat.CloudEventsBinary` (`"cloudevents+binary"`) format
    constant in `Hermodr.Publisher`.
  - New `CloudEventsBinarySerializer` — emits the raw event `data` as the
    request body via `JsonEventFormatter.EncodeBinaryModeEventData`.
  - New `CloudEventHttpHeadersMapper` (internal) — projects every CloudEvent
    context attribute (including extensions) onto `ce-*` HTTP headers per the
    [CloudEvents HTTP binding §3.1](https://github.com/cloudevents/spec/blob/main/cloudevents/bindings/http-protocol-binding.md#31-binary-content-mode).
  - The channel sets the request `Content-Type` to the event's
    `datacontenttype` (falling back to `application/json`), so `data` may be
    any content type (e.g. `application/protobuf`).
  - Batch delivery in binary mode throws `NotSupportedException` (the HTTP
    binding defines no binary batch).
  - HMAC signing still covers the raw body bytes only; `IWebhookSignatureProvider`
    is unchanged.

- **WebHook abuse-protection discovery** ([CloudEvents HTTP WebHooks spec](https://github.com/cloudevents/spec/blob/main/cloudevents/http-webhook.md)):
  - New `WebhookDiscoveryOptions` (`RequestOrigin`, `RequestCallback`,
    `RequestRate`, `RequestTimeout`, `RequireSuccessfulHandshake`) on
    `WebhookPublishOptions.Discovery`.
  - New `WebhookHandshakeClient` (internal) — lazily issues the `OPTIONS`
    pre-flight before the first delivery, caches the granted
    `WebHook-Allowed-Origin` / `WebHook-Allowed-Rate` per endpoint, and gates
    concurrent first deliveries to a single round-trip.
  - `WebHook-Request-Origin` attached to every delivery POST after a
    successful handshake (spec §2.1).
  - New `WebhookDiscoveryPermission` (cached handshake result, queryable by
    the host) and `WebhookHandshakeException` (strict-mode refusal/failure).
  - Best-effort (`RequireSuccessfulHandshake = false`, default) logs refusal
    and proceeds; strict (`true`) throws and suppresses the POST.
  - The granted `WebHook-Allowed-Rate` is tagged on the publish `Activity`
    but **not auto-enforced** — the host applies its own throttling policy.
  - The publisher does **not** host the `WebHook-Request-Callback` endpoint;
    it only advertises the URL for the receiver to call.

- **`Retry-After` on 429** now honored — when a delivery returns
  `429 Too Many Requests` with a `Retry-After` header (delta-seconds or
  HTTP-date), the channel uses it as the next retry delay, overriding the
  configured exponential backoff for that attempt (spec §2.2).

### ⚠ Breaking Changes

- **None** — the legacy `"json"` default wire format is preserved
  (regression-guarded); `Discovery` defaults to `null` (no handshake, no
  `WebHook-Request-Origin` header). All signing, retry, and
  subscriber-management behaviour is unchanged.

**Full Changelog**: https://github.com/deveel/hermodr/compare/v1.4.0...v1.5.0

## v1.4.0 - Schema Governance & AsyncAPI Export

### 🚀 Features

- **AsyncAPI & Schema Export Improvements** (roadmap item 11, v1.4.0):
  - **Channel metadata abstraction** in `Hermodr.Publisher`: new
    `IEventPublishChannelMetadata` interface, `EventTransports` well-known
    transport string constants, `IChannelMetadataSource` marker for options
    classes, and `EventPublishChannelMetadata.For(...)` resolver that unwraps
    `NamedChannelDecorator` and reads metadata from either the channel or its
    options. `EventPublisher.GetChannelMetadata()` exposes the live pipeline
    metadata for tooling.
  - **Transport options implement `IChannelMetadataSource`**: `RabbitMqPublishOptions`,
    `ServiceBusPublishOptions`, and `WebhookPublishOptions` now expose
    transport-aware metadata (`exchange`, `routingKey`, `queue`, `url`, ...).
    Storage / deferral / recovery channels (Audit Trail, Outbox, Dead-Letter)
    report `EventTransports.Other` and are excluded from exported contracts.
  - **Auto-discovery** (`Hermodr.Schema.AsyncApi`): new `EventSchemaDiscovery`
    service that scans assemblies for `[Event]`-annotated types with a
    `DataVersion` and returns a schema for each — the export-time counterpart of
    `EventSchemaRegistry.ScanAssembly`.
  - **Enriched AsyncAPI 2.x document**: new `AsyncApiDocumentBuilder` fluently
    builds a document from schemas + channel metadata, populates RabbitMQ (AMQP)
    and HTTP channel bindings, aggregates servers, and emits a publish operation
    per channel. New `AddEvent` overload on `EventSchemaAsyncApiExtensions` enriches
    a single channel with transport-specific bindings.
  - **OpenAPI 3.1 webhook export** (new package `Hermodr.Schema.OpenApi`):
    converts `IEventSchema` to `OpenApiSchema`, builds a `webhooks` document with
    one `POST` operation per event type whose request body references the schema
    under `components/schemas`, and preserves the transport identifier via the
    `x-hermodr-transport` operation extension. JSON and YAML writers
    (`EventSchemaOpenApiWriter`, `EventSchemasOpenApiWriter`) provided.
  - **`dotnet` global tool** (new package `Hermodr.AsyncApi.Exporter.Tool`):
    exports AsyncAPI 2.x or OpenAPI 3.1 documents from one or more assemblies
    without a running host, with `--channels-file` (JSON) and `--entry
    <type>::<method>` mechanisms to supply transport metadata. Installable via
    `dotnet tool install --global Hermodr.AsyncApi.Exporter.Tool`.

### 📖 Documentation

- New docs pages: [Export as OpenAPI 3.1](docs/schema/export/openapi.md),
  [CLI Export Tool](docs/schema/export/cli-tool.md), and updated
  [Export Formats Overview](docs/schema/export/README.md).
- New Docusaurus version snapshot `v1.4.0` (latest); removed the unreleased
  `v1.3.0` snapshot.

### 🛠 Refactoring

- **Exporter library extraction** (PR #100): the `hermodr-asyncapi` CLI was
  refactored from a monolithic `Program.cs` into a packable internal library
  (`Hermodr.AsyncApi.Exporter`) and a slim Spectre.Console.Cli shell
  (`Hermodr.AsyncApi.Exporter.Tool`). The tool now single-targets `net10.0`
  with `RollForward=LatestMajor` (tools cannot multi-target), and the library
  is embedded in the nuspkg via `PrivateAssets=all` — the install is fully
  self-contained. The dual-purpose `samples/asyncapi-export` Web app was
  removed and replaced by a dedicated non-test fixture project
  (`Hermodr.AsyncApi.Exporter.Fixtures`).

**Full Changelog**: https://github.com/deveel/hermodr/compare/v1.2.9...v1.4.0

## v1.2.9 - Schema Compatibility & Event Upcasting

### 🚀 Features

- **Schema Compatibility Checking**: New `IEventSchemaCompatibilityChecker` and `EventSchemaCompatibilityChecker` in `Hermodr.Schema` that validates whether an event can be safely upcasted to a target schema version by analyzing schema changes (additions, removals, modifications) against configurable `SchemaCompatibilityLevel` (Backward, Forward, Full, None)
- **Event Upcasting Pipeline**: New `EventUpcastingPipeline` and `EventUpcasterRegistry` supporting ordered chains of `IEventUpcaster` implementations with JSON data adapter (`JsonEventUpcastingDataAdapter`) for transforming event data across schema versions
- **Publish-Time Upcasting Middleware**: New `EventUpcastingMiddleware` in `Hermodr.Publisher` that automatically applies registered upcasters during the publish pipeline, with configurable `MissingUpcasterBehavior` (Throw/Skip)
- **Version-Aware Subscription Routing**: New `EventFilter`, `EventFilterBuilder`, and `EventFilterEvaluator` in `Hermodr.Subscriptions` enabling subscription-side filtering based on event type version and data version

### Change Log

#### Hermodr.Schema
- `IEventSchemaCompatibilityChecker` — new interface for schema compatibility validation
- `EventSchemaCompatibilityChecker` — implementation analyzing schema changes against compatibility levels
- `SchemaCompatibilityLevel` — enum (Backward, Forward, Full, None)
- `SchemaCompatibilityResult` — result type with pass/fail and change descriptions
- `SchemaChange` — record type describing a single schema change
- `SchemaChangeType` — enum (PropertyAdded, PropertyRemoved, PropertyTypeChanged, PropertyRequiredChanged)
- `IEventUpcaster` — new interface for transforming event data across schema versions
- `IEventUpcasterRegistry` — new interface for registering and resolving upcasters
- `EventUpcasterRegistry` — default implementation with ordered upcaster chains
- `IEventUpcastingPipeline` — new interface for executing upcaster chains
- `EventUpcastingPipeline` — default pipeline implementation
- `IEventUpcastingDataAdapter` — new interface for format-specific data access during upcasting
- `JsonEventUpcastingDataAdapter` — JSON implementation using `JsonDocument`/`JsonElement`
- `UpcastContext` — context object passed through the upcasting pipeline
- `EventUpcastingException` — exception type for upcasting failures
- `ServiceCollectionExtensions` — updated with upcaster registration methods

#### Hermodr.Publisher
- `EventUpcastingMiddleware` — new middleware applying registered upcasters during publish
- `EventUpcastingOptions` — configuration for upcasting behavior
- `MissingUpcasterBehavior` — enum (Throw, Skip) for handling missing upcasters
- `EventPublisherBuilderExtensions` — new `UseEventUpcasting()` fluent extension
- `EventFactory` — updated with version-aware event creation helpers

#### Hermodr.Subscriptions
- `EventFilter` — new type for subscription-side version filtering
- `EventFilterBuilder` — fluent builder for constructing event filters
- `EventFilterEvaluator` — evaluates filters against incoming events

**Full Changelog**: https://github.com/deveel/hermodr/compare/v1.2.8...v1.2.9

## v1.2.8 - Schema Validation Middleware

### 🚀 Features

- **Schema Validation Middleware**: New `SchemaValidationMiddleware` in `Hermodr.Publisher` that validates CloudEvents envelope attributes (`id`, `source`, `type`, `specversion`) before channel dispatch, throwing `InvalidCloudEventException` if any are absent (#93)
- **AuditTrailPublishChannel Refactor**: Refactored `AuditTrailPublishChannel` to use `IServiceProvider` for dependency resolution, improving testability and DI integration (#93)
- **Kista Dependency Updates**: Updated references to the Kista provider model across the framework (#93)

### 📖 Documentation

- **Documentation Restructure**: Comprehensive docs reorganization with versioning support (#92)
  - New Reliability Patterns category (Outbox, Dead-Letter, Delivery Log, Audit Trail)
  - Event Schema category promoted higher in sidebar
  - Docusaurus versioning configuration added
  - Versioned docs snapshots created for tags v1.2.3 through v1.2.7
  - Version dropdown in navbar for easy switching between doc versions

**Full Changelog**: https://github.com/deveel/hermodr/compare/v1.2.7...v1.2.8

## v1.2.7 - NDJson Delivery Log & Kista Migration

### 🚀 Features

- **NDJson Delivery Log Options**: New NDJson delivery log backend with configurable file rolling, retention policies, and in-memory storage implementation (#91)
- **Kista Provider Migration**: Migrated framework components from Deveel to Kista provider model, aligning with the new infrastructure abstraction (#91)

### 📖 Documentation

- Updated installation and README for new Kista dependencies
- Added frameworks comparison table

**Full Changelog**: https://github.com/deveel/hermodr/compare/v1.2.6...v1.2.7

## v1.2.6 - OpenTelemetry Enhancements

### 🚀 Features

- **Unified Diagnostics Infrastructure**: New centralized telemetry and diagnostics system spanning all publisher and subscription components (#89)
  - `HermodrDiagnostics` — central `ActivitySource` and `Meter` for the entire pipeline
  - `PublisherTelemetry`, `ChannelTelemetry`, `SubscriptionTelemetry` — per-component telemetry wrappers
  - `PipelineDiagnosticsMiddleware` / `PipelineDiagnosticsTelemetry` — pipeline-level diagnostics middleware
  - `TelemetryConstants` promoted from `Hermodr.Publisher.OpenTelemetry` to core `Hermodr.Publisher` for cross-project reuse
- **Per-Transport Instrumentation**: Every transport channel now emits consistent OpenTelemetry traces and metrics
  - New telemetry classes: `AuditTrailTelemetry`, `ServiceBusTransportTelemetry`, `DeadLetterTelemetry`, `MassTransitTransportTelemetry`, `OutboxTelemetry`, `RabbitMqTransportTelemetry`, `WebhookTransportTelemetry`
  - All existing publish channels instrumented: `AuditTrailPublishChannel`, `ServiceBusPublishChannel`, `DeadLetterMessageReplayer`, `DeadLetterReplayProcessor`, `DeadLetterStorageHandler`, `MassTransitPublishChannel`, `OutboxPublishChannel`, `OutboxRelayProcessor`, `RabbitMqPublishChannel`, `WebhookPublishChannel`
- **New `OpenTelemetryBuilderExtensions`** — fluent extensions for configuring diagnostics on the event pipeline

### 🧪 Tests

- 15 new test files and significant extensions to existing tests covering the full telemetry surface:
  - `ChannelMetricsTests`, `HermodrDiagnosticsTests`, `OpenTelemetryBuilderExtensionsTests`
  - `PipelineDiagnosticsMiddlewareTests`, `PipelineDiagnosticsTelemetryTests`
  - `PublisherMetricsTests`, `SubscriptionMetricsTests`
  - `TelemetryConstantsTests`, `TelemetryEdgeCasesTests`, `TelemetryMetricsTests`, `TelemetryTagsTests`
  - Extended: `HermodrTelemetryTests`, `EndToEndTracePropagationTests`
  - Removed obsolete `MetricsMiddlewareTests`

### Change Log
_(Detailed breakdown of every file and type changed in this release)_

#### Hermodr.Publisher (Core)
- `HermodrDiagnostics` — new central `ActivitySource` and `Meter` for publisher diagnostics
- `PublisherTelemetry` — new publisher-level telemetry wrapper
- `ChannelTelemetry` — new channel-level telemetry wrapper
- `TelemetryConstants` — moved from `Hermodr.Publisher.OpenTelemetry` to core; defines activity source names, meter names, and tag keys
- `EventPublisher` — refactored to emit diagnostics through `HermodrDiagnostics`
- `EventPublishChannel` — refactored to emit channel-level traces and metrics

#### Hermodr.Subscriptions
- `SubscriptionTelemetry` — new subscription-level telemetry wrapper
- `EventDispatcher` — refactored to emit dispatch traces and metrics through `SubscriptionTelemetry`

#### Hermodr.Publisher.OpenTelemetry
- `OpenTelemetryBuilderExtensions` — new fluent extensions for wiring diagnostics
- `PipelineDiagnosticsMiddleware` / `PipelineDiagnosticsTelemetry` — new middleware for pipeline-level diagnostics
- `HermodrTelemetry` — refactored to delegate to core diagnostics
- `TelemetryMetrics` — refactored to use core meter
- `MetricsOptions` — modified to align with new diagnostics
- `OpenTelemetryPublishMiddleware`, `OpenTelemetrySubscriptionMiddleware` — refactored for new diagnostics pipeline
- `TelemetryConstants` — removed (moved to core `Hermodr.Publisher`)

#### Hermodr.Publisher.AuditTrail
- `AuditTrailTelemetry` — new telemetry class
- `AuditTrailPublishChannel` — instrumented with `AuditTrailTelemetry`

#### Hermodr.Publisher.AzureServiceBus
- `ServiceBusTransportTelemetry` — new telemetry class
- `ServiceBusPublishChannel` — instrumented with `ServiceBusTransportTelemetry`

#### Hermodr.Publisher.DeadLetter
- `DeadLetterTelemetry` — new telemetry class
- `DeadLetterMessageReplayer` — instrumented with `DeadLetterTelemetry`
- `DeadLetterReplayProcessor` — instrumented with `DeadLetterTelemetry`
- `DeadLetterStorageHandler` — refactored to use `DeadLetterTelemetry`

#### Hermodr.Publisher.MassTransit
- `MassTransitTransportTelemetry` — new telemetry class
- `MassTransitPublishChannel` — instrumented with `MassTransitTransportTelemetry`

#### Hermodr.Publisher.Outbox
- `OutboxTelemetry` — new telemetry class
- `OutboxPublishChannel` — refactored to use `OutboxTelemetry`
- `OutboxRelayProcessor` — refactored to use `OutboxTelemetry`

#### Hermodr.Publisher.RabbitMq
- `RabbitMqTransportTelemetry` — new telemetry class
- `RabbitMqPublishChannel` — instrumented with `RabbitMqTransportTelemetry`

#### Hermodr.Publisher.Webhook
- `WebhookTransportTelemetry` — new telemetry class
- `WebhookPublishChannel` — instrumented with `WebhookTransportTelemetry`

#### Tests (Hermodr.Publisher.OpenTelemetry.XUnit)
- `ChannelMetricsTests` — channel-level metrics validation
- `HermodrDiagnosticsTests` — diagnostics source and meter tests
- `HermodrTelemetryTests` — extended telemetry coverage
- `OpenTelemetryBuilderExtensionsTests` — builder extension validation
- `PipelineDiagnosticsMiddlewareTests` — middleware behavior tests
- `PipelineDiagnosticsTelemetryTests` — pipeline telemetry validation
- `PublisherMetricsTests` — publisher-level metrics tests
- `SubscriptionMetricsTests` — subscription-level metrics tests
- `TelemetryConstantsTests` — constant and tag key validation
- `TelemetryEdgeCasesTests` — edge case coverage for telemetry
- `TelemetryMetricsTests` — metrics recording validation
- `TelemetryTagsTests` — tag propagation tests
- `EndToEndTracePropagationTests` — extended to include new diagnostics paths
- `MetricsMiddlewareTests` — removed (superseded by new diagnostics)
- AssemblyInfo and project file updates

**Full Changelog**: https://github.com/deveel/hermodr/compare/v1.2.5...v1.2.6

---

## v1.2.5 (2026-06-04)

### 🚀 Features

- **Audit Trail Subsystem**: Five new packages providing an append-only event audit trail with multiple storage backends and publisher integration (#82)
  - `Hermodr.AuditTrail` — core interfaces and types for writing, reading, and querying audit trail entries
  - `Hermodr.AuditTrail.InMemory` — in-memory audit trail backend for testing and lightweight scenarios
  - `Hermodr.AuditTrail.EntityFramework` — EF Core backend for persisting audit trail entries to relational databases
  - `Hermodr.AuditTrail.NDJson` — file-based backend using newline-delimited JSON with automatic file rolling, retention policies, and a pluggable filesystem abstraction
  - `Hermodr.Publisher.AuditTrail` — publisher integration channel that transparently records events to any configured audit trail backend

### 🧪 Tests

- 202 unit and integration tests across all five packages
- NDJson backend: 54 tests covering append/read, file rolling, retention, filtering, and concurrent I/O
- EF Core backend: SQLite integration tests for the full entry lifecycle
- Publisher channel: tests for recording, bypass, and typed/untyped routing

### 📖 Documentation

- New NDJson audit trail sample with REST query endpoints
- Comprehensive package reference in `docs/packages.md`
- README and ROADMAP updated with compliance and audit trail features

### Change Log
_(Detailed breakdown of every file and type added in this release)_

#### Hermodr.AuditTrail (Core)
- `IAuditTrailWriter`, `IAuditTrailReader<TEntry>`, `IAuditTrailEntry` interfaces
- `AuditTrailEntry` — default implementation with `FromCloudEvent()` factory
- `AuditTrailBuilder` — fluent DI registration builder
- `AuditTrailStreamQuery` — filter criteria (type, source, time range, attributes)
- `AuditTrailPageQueryExtensions` — 7 LINQ-style filter methods for paged queries

#### Hermodr.AuditTrail.InMemory
- `InMemoryAuditTrail` — concurrent in-memory storage (writer + reader)
- `InMemorySharedBag<TEntry>` — thread-safe shared entry store
- `UseInMemory()` / `AddInMemoryAuditTrail()` / `AddAuditTrailQuerying()` extensions

#### Hermodr.AuditTrail.EntityFramework
- `AuditTrailDbContext` — EF Core DbContext with audit trail entity sets
- `DbAuditTrailEntry` / `DbAuditTrailAttribute` — EF entities with converters
- `EntityAuditTrailRepository` — filtered streaming via `IQueryable`
- `EntityAuditTrail` — EF Core writer + reader implementation
- `UseEntityFramework()` / `AddEntityFrameworkAuditTrail()` extensions

#### Hermodr.AuditTrail.NDJson
- `NdJsonAuditTrail` — NDJSON file storage (writer + reader + IDisposable)
  - Auto file rolling by size (`MaxFileSizeBytes`, default 10MB) or time interval (`RollInterval`)
  - Retention cleanup (`MaxFileCount`, default 30 files)
  - Pluggable `IFileSystem` (backed by `System.IO.Abstractions`)
  - Async streaming reads with concurrent writer support
- `NdJsonAuditTrailOptions` — configurable directory, file naming, buffer size
- `UseNDJson()` / `AddNDJsonAuditTrail()` / `AddNDJsonAuditTrailQuerying()` extensions

#### Hermodr.Publisher.AuditTrail
- `AuditTrailPublishChannel` / `AuditTrailPublishChannel<TEvent>` — records published events to audit trail
- `BypassAuditTrailOptions` — prevents infinite loops during replay/relay
- 4 overloads of `AddAuditTrail()` on `EventPublisherBuilder`

#### Tests
- **Hermodr.AuditTrail.XUnit**: 44 tests (builder, entries, page queries, stream queries, edge cases)
- **Hermodr.AuditTrail.InMemory.XUnit**: 43 tests (integration, shared bag, edge cases, DI registration)
- **Hermodr.AuditTrail.EntityFramework.XUnit**: 32 tests (EF entities, repository, SQLite integration)
- **Hermodr.AuditTrail.NDJson.XUnit**: 54 tests (append/read, rolling, retention, pluggable filesystem, cancellation, concurrent I/O)
- **Hermodr.Publisher.AuditTrail.XUnit**: 29 tests (channels, typed routing, builder, bypass scenarios)

#### Samples
- `samples/audit-trail/` — EF Core with SQLite, order processing scenario, 6 REST endpoints
- `samples/audit-trail-ndjson/` — NDJson with file rolling, order processing scenario, 4 REST endpoints

#### Documentation
- New: `docs/samples/audit-trail-ndjson.md`
- Updated: `docs/packages.md`, `docs/SUMMARY.md`, `README.md`, `ROADMAP.md`

**Full Changelog**: https://github.com/deveel/hermodr/compare/v1.2.4...v1.2.5

---

## v1.2.4 (2026-05-24)

### ⚠ Breaking Changes

- **Project Rename**: The project has been renamed from `Deveel.Events` to **Hermodr**, giving it a new identity disconnected from the Deveel brand (#80).
  - All package names changed from `Deveel.Events.*` to `Hermodr.*`
  - All namespaces reorganized to match the new naming
  - Solution file renamed from `Deveel.Events.sln` to `Hermodr.sln`
  - Migration path: update package references and `using` directives accordingly

### 🚀 Features

- **OpenTelemetry Support**: New `Hermodr.Publisher.OpenTelemetry` package providing distributed tracing, metrics collection, and W3C trace context propagation via CloudEvents extensions (#81)
  - `OpenTelemetryPublishMiddleware` — injects trace context into outgoing CloudEvents
  - `OpenTelemetrySubscriptionMiddleware` — restores parent trace context on incoming events
  - `MetricsMiddleware` — collects publishing metrics (count, duration, errors) per event type
  - Full end-to-end trace correlation across service boundaries
- **Re-export of packages**: All existing `Deveel.Events.*` packages are now published under the `Hermodr.*` brand

### 🧪 Tests

- Comprehensive unit and integration tests for OpenTelemetry instrumentation:
  - `HermodrTelemetryTests`, `MetricsMiddlewareTests`, `OpenTelemetryPublishMiddlewareTests`
  - `OpenTelemetrySubscriptionMiddlewareTests`, `OpenTelemetryBuilderExtensionsTests`
  - End-to-end trace propagation tests
- All tests updated for namespace compliance after the rename

### 📖 Documentation

- New OpenTelemetry instrumentation guide (`docs/publishers/opentelemetry.md`)
- New sample project: `OrderService.OpenTelemetry` demonstrating distributed tracing setup
- All docs updated to reflect the Hermodr rename and new package names

**Full Changelog**: https://github.com/deveel/hermodr/compare/v1.2.3...v1.2.4

---

## v1.2.3 (2026-05-23)

### 🚀 Features
- **Delivery Log**: New packages `Hermodr.Publisher.DeliveryLog` and
  `Hermodr.Publisher.DeliveryLog.EntityFramework` — middleware, in-memory,
  NDJSON file, and EF Core backends for recording event delivery attempts (#79)
- **Scheduled Delivery**: Support for scheduled event delivery in outbox pattern (#77)

### 🧪 Tests
- Unit tests for dead-letter message handling and outbox message management
- Replaced delay-based assertions with task completions and polling

### 📖 Documentation
- README comparison of Hermodr and other .NET messaging frameworks
- DDD lifecycle management, Deveel.Repository, and test coverage tooling references

**Full Changelog**: https://github.com/deveel/hermodr/compare/v1.2.2...v1.2.3

---

## v1.2.2 (2026-05-06)

### 🚀 Features
- **Dead Letter & Replay**: New packages `Hermodr.Publisher.DeadLetter` and
  `Hermodr.Publisher.DeadLetter.EntityFramework` — capture failed messages and
  replay them through configurable pipelines (#76)
- **Outbox EF Core**: Entity Framework Core support for the outbox pattern

### 📖 Documentation
- Dead letter concepts, configuration, and replay workflows
- Outbox integration with EF Core

**Full Changelog**: https://github.com/deveel/hermodr/compare/v1.2.1...v1.2.2

---

## v1.2.1 (2026-05-02)

### 🚀 Features
- **Transactional Outbox Pattern**: New `Hermodr.Publisher.Outbox` package (#65)
  - Out-of-process relay with `OutboxRelayService`
  - EF Core storage via `Hermodr.Publisher.Outbox.EntityFramework`
  - Configurable relay intervals, batch sizes, and retry policies

### 🧪 Tests
- Integration tests for SQLite and MySQL outbox repositories

### 📖 Documentation
- Outbox pattern concepts, configuration, and relay setup
- Framework integration guides for MediatR, Wolverine, and Brighter

**Full Changelog**: https://github.com/deveel/hermodr/compare/v1.2.0...v1.2.1

---

## v1.2.0 (2026-05-02)

### 🚀 Features
- Azure Service Bus publisher with connection factory abstraction
- RabbitMQ publisher with enhanced message factory
- Dead letter error handler for publish failures
- Package consolidation and dependency updates

### 📖 Documentation
- First-class documentation site with MkDocs
- Quick-start guides for all publishers
- Versioning strategy document

**Full Changelog**: https://github.com/deveel/hermodr/compare/v1.1.0...v1.2.0

---

## v1.1.0 (2026-04-30)

### 🚀 Features
- **Subscriptions & Routing**: New `Hermodr.Subscriptions` package
  - Event filtering with expression-based filters
  - Publisher-side routing via `IRoutingEventSubscription`
  - JSON event data deserialization
- Webhook publisher with HMAC signature support
- MassTransit publish channel

### 🧪 Tests
- Subscription filter evaluation and routing tests
- Webhook signature provider tests

**Full Changelog**: https://github.com/deveel/hermodr/compare/v1.0.0...v1.1.0

---

## v1.0.0 (2026-04-26)

### 🚀 Features
- Core `Hermodr.Publisher` library with:
  - CloudEvents-compliant event publishing pipeline
  - Named channel routing and middleware pipeline
  - JSON/XML serialization
  - Synchronous and scheduled publishing
- `Hermodr.Annotations` for event metadata via attributes
- `Hermodr.Schema` for event schema definition, validation, and export (JSON, YAML, AsyncAPI)
- `Hermodr.Schema.AsyncApi` — AsyncAPI 2.x document generation
- `Hermodr.Schema.Yaml` — YAML schema serialization
- `Hermodr.TestPublisher` — in-memory test channel for unit testing
- `Hermodr.Publisher.RabbitMq` — AMQP 0-9-1 publisher
- `Hermodr.Publisher.Webhook` — HTTP webhook publisher with HMAC signing
- `Hermodr.Publisher.MassTransit` — MassTransit transport integration
- `Hermodr.Publisher.AzureServiceBus` — Azure Service Bus integration
- `Hermodr.Subscriptions` — event filtering and routing
- `Hermodr.Publisher.DeadLetter` — dead-letter error handling
- `Hermodr.Publisher.DeliveryLog` — delivery attempt logging

### 🧪 Tests
- Full unit and integration test suite across all packages

### 📖 Documentation
- Comprehensive documentation site with MkDocs material
- Quick-start guide, concepts, samples, and package reference
