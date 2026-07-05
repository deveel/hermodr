# Changelog

## [Unreleased]

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
