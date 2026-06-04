# Changelog

## Unreleased

### 🚀 Features

- **NDJson Audit Trail**: New `Hermodr.AuditTrail.NDJson` package providing a newline-delimited JSON (NDJSON) file storage backend for the audit trail, with automatic file rolling by size or time interval, retention policies, and a **pluggable filesystem abstraction** built on top of `System.IO.Abstractions`. By registering a different `IFileSystem` implementation, the local disk can be replaced with Azure Blob Storage, Amazon S3, or any other compatible storage backend without touching the storage code.
  - `NdJsonAuditTrail` — implements both `IAuditTrailWriter` and `IAuditTrailReader<AuditTrailEntry>` on a single class (mirrors `InMemoryAuditTrail`)
  - `NdJsonAuditTrailOptions` — `DirectoryPath`, `MaxFileSizeBytes`, `RollInterval`, `MaxFileCount`, `FileNamePrefix`, `FileExtension`, `ReadBufferSize`
  - `AuditTrailBuilder.UseNDJson(...)` — fluent builder extension for the publisher integration
  - `AddNDJsonAuditTrail(...)` and `AddNDJsonAuditTrailQuerying(...)` — top-level `IServiceCollection` extensions
  - **Async streaming reads**: `ReadAsync` now opens files with `FileShare.ReadWrite` and `FileOptions.Asynchronous | SequentialScan`, reads each NDJSON file line-by-line via `StreamReader.ReadLineAsync`, applies the `AuditTrailStreamQuery` filter while streaming (no full pre-read), and yields matched entries in chronological order. Concurrent writers are never blocked by an in-progress read, and the file content is never fully buffered in memory. Mid-read file deletion / directory deletion is handled gracefully (logged and skipped, the read completes).
  - 54 unit tests covering construction, append/read round-trip, file rolling, retention cleanup, all `AuditTrailStreamQuery` filters, cancellation, the `IFileSystem` abstraction (using `MockFileSystem` from `System.IO.Abstractions.TestingHelpers`), `ProviderName` and `IDisposable` contracts, async streaming yield, concurrent read/write non-blocking, file/directory deletion resilience, and custom `ReadBufferSize`
  - New sample: [`samples/audit-trail-ndjson/AuditTrail.NDJson.Sample`](https://github.com/deveel/hermodr/tree/main/samples/audit-trail-ndjson/AuditTrail.NDJson.Sample)
  - New docs: [`docs/samples/audit-trail-ndjson.md`](https://github.com/deveel/hermodr/blob/main/docs/samples/audit-trail-ndjson.md) and package details in [`docs/packages.md`](https://github.com/deveel/hermodr/blob/main/docs/packages.md)

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
