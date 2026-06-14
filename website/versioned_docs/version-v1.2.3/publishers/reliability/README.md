# Reliability Patterns

Hermodr provides a comprehensive set of **reliability patterns** that ensure your domain events are delivered reliably, tracked accurately, and can be recovered in case of failures.

## Why Reliability Matters

In distributed systems, publishing an event is rarely as simple as calling a method. Network failures, broker outages, serialization errors, and resource constraints can all cause event delivery to fail. The reliability patterns in Hermodr address these challenges:

| Pattern | Problem Solved | When to Use |
|---------|---------------|-------------|
| **[Transactional Outbox](outbox/README.md)** | Prevents event loss when the broker is unavailable | When you need atomic commits between business data and event publication |
| **[Dead-Letter Handling](dead-letter/README.md)** | Captures failed deliveries for later recovery | When transient failures must not result in permanent event loss |
| **[Delivery Log](delivery-log/README.md)** | Provides operational visibility into publish health | When you need to monitor delivery success rates, latency, and troubleshoot issues |
| **[Audit Trail](audit-trail/README.md)** | Creates an immutable record of all published events | For compliance, auditing, and reconstructing event history |

## Architecture Overview

```
┌────────────────────────────────────────────────────────────────────┐
│                    EventPublisher Pipeline                         │
│                                                                    │
│   ┌──────────────┐      ┌────────────────────────────────┐         │
│   │  Event Data  │─────▶│      Middleware Pipeline       │         │
│   └──────────────┘      └───────────────┬────────────────┘         │
│                                         │                          │
│              ┌──────────────────────────┼───────────────────┐      │
│              │                          │                   │      │
│     ┌────────▼────────┐    ┌────────────▼──────┐  ┌─────────▼────┐ │
│     │  Outbox Channel │    │  Delivery Log     │  │  Audit       │ │
│     │  (durability)   │    │  (telemetry)      │  │  Trail       │ │
│     └────────┬────────┘    └───────────────────┘  └──────────────┘ │
│              │                                                     │
│     ┌────────▼────────┐                                            │
│     │  Relay Service  │                                            │
│     │  (async send)   │                                            │
│     └────────┬────────┘                                            │
│              │                                                     │
│     ┌────────▼────────┐                                            │
│     │  Dead-Letter    │                                            │
│     │  (failure capture)                                           │
│     └─────────────────┘                                            │
└────────────────────────────────────────────────────────────────────┘
```

## Pattern Selection Guide

| Scenario | Recommended Pattern | Key Benefit |
|----------|---------------------|-------------|
| Broker is unavailable at publish time | **[Transactional Outbox](outbox/README.md)** | Atomic commits between business data and events |
| Transient failures cause message loss | **[Dead-Letter Handling](dead-letter/README.md)** | Automatic retry and failure recovery |
| Need to monitor delivery health | **[Delivery Log](delivery-log/README.md)** | Track success rates, latency, and troubleshoot |
| Compliance requires immutable audit trail | **[Audit Trail](audit-trail/README.md)** | Complete, tamper-proof event history |
| Need maximum reliability | **All patterns combined** | Durability + telemetry + recovery + compliance |

### Combining Patterns

The patterns are designed to work together:

| Pattern | Role in Combined Stack |
|---------|------------------------|
| **Outbox** | Ensures durability — events persisted atomically with business data |
| **Delivery Log** | Tracks every attempt — success, failure, retries, latency |
| **Dead-Letter** | Captures failures — automatic retry and recovery |
| **Audit Trail** | Maintains history — immutable record for compliance |

## Common Combinations

### Maximum Reliability Stack
```csharp
builder.Services
    .AddEventPublisher()
    // Durability: persist events atomically with business data
    .AddEntityFrameworkOutbox(opts => opts.UseSqlServer(connectionString))
    .WithRelay()
    // Telemetry: track every delivery attempt
    .AddDeliveryLog(log => log.UseEntityFramework(opts => 
        opts.UseSqlServer(connectionString)))
    // Recovery: capture and retry failed deliveries
    .AddDeadLetter()
    .WithEntityFramework(opts => opts.UseSqlServer(connectionString))
    .WithReplayWorker()
    // Audit: maintain immutable event history
    .AddAuditTrail(audit => audit.UseNDJson(options =>
        options.DirectoryPath = "/var/audit"));
```

### Lightweight Monitoring
```csharp
builder.Services
    .AddEventPublisher()
    // Just track delivery attempts
    .AddDeliveryLog(log => log.UseInMemory());
```

### Compliance-First
```csharp
builder.Services
    .AddEventPublisher()
    // Audit trail for compliance
    .AddAuditTrail(audit => audit.UseNDJson(options =>
    {
        options.DirectoryPath = "/secure/audit";
        options.MaxFileCount = 365; // Keep 1 year
    }))
    // Delivery log for operational visibility
    .AddDeliveryLog(log => log.UseEntityFramework(opts =>
        opts.UseSqlServer(connectionString)));
```

## Related Pages

- [Publisher Channels Overview](../README.md)
- [Error Handling](../error-handling.md)
- [OpenTelemetry Instrumentation](../opentelemetry.md)
