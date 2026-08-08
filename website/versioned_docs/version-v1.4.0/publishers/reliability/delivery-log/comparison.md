# Delivery Log Comparison

The Delivery Log is one of several reliability features in Hermodr. Understanding how they differ helps you choose the right tool for each scenario.

## Feature Comparison

| Feature | Scope | Primary Use Case | Persistence | Query Capability |
|---------|-------|------------------|-------------|------------------|
| **Delivery Log** | Attempt metadata per publish | Operational visibility into publish health | Configurable (In-Memory, NDJSON, EF) | Full querying, aggregations |
| **Dead-Letter** | Failed event payloads + replay | Recovering from delivery failures | Configurable (EF Core) | Replay workflow |
| **Audit Trail** | Domain fact audit trail | Compliance, read-model rebuilding | NDJSON, EF Core | Historical queries |
| **Error Handling** | Pipeline error interception | Custom error policies (logging, circuit-breaker) | None (in-memory callbacks) | None |
| **OpenTelemetry** | Trace context propagation | End-to-end distributed tracing | External (Jaeger, AppInsights) | Distributed tracing UI |

---

## Delivery Log vs Dead-Letter

### Delivery Log

**What it does:**
- Records **metadata** about every publish attempt (success or failure)
- Captures: timestamp, channel, attempt number, outcome, error details, elapsed time
- Answers: "How many times did we try?", "How long did it take?", "What's the success rate?"

**What it doesn't do:**
- Doesn't preserve the event payload for replay
- Doesn't automatically retry failed deliveries

**Best for:**
- Monitoring delivery health
- Computing SLAs
- Troubleshooting delivery issues
- Identifying problematic channels or event types

### Dead-Letter

**What it does:**
- Captures **failed events** for later recovery
- Preserves the full `CloudEvent` payload
- Provides replay mechanisms (manual or automatic)
- Answers: "Which events failed?", "Can we retry them?"

**What it doesn't do:**
- Doesn't track successful deliveries
- Doesn't provide detailed timing metrics

**Best for:**
- Ensuring no events are lost
- Recovering from transient failures
- Manual inspection of failed events

### Use Together

The Delivery Log and Dead-Letter are **complementary**:

```csharp
builder.Services
    .AddEventPublisher()
    // Track every attempt
    .AddDeliveryLog(log => log.UseEntityFramework(opts => 
        opts.UseSqlServer(connectionString)))
    // Capture and retry failures
    .AddDeadLetter()
    .WithEntityFramework(opts => opts.UseSqlServer(connectionString))
    .WithReplayWorker();
```

**Scenario:**
1. Event publish fails
2. **Dead-Letter** captures the event payload for replay
3. **Delivery Log** records the failure metadata (when, which channel, error type, latency)
4. Operators query the Delivery Log to see failure patterns
5. Dead-Letter worker retries the event automatically

---

## Delivery Log vs Audit Trail

### Delivery Log

**What it does:**
- Records **publish attempt telemetry**
- Focus: operational health of the publishing infrastructure
- One record per **attempt** (same event may have multiple records if retried)

**Best for:**
- "Did this event get delivered successfully?"
- "How long did delivery take?"
- "Which channel is failing most often?"

### Audit Trail

**What it does:**
- Records **domain facts** (the events themselves)
- Focus: business auditing, compliance, event history
- One record per **event** (regardless of delivery attempts)

**Best for:**
- "What events occurred in the system?"
- "Show me the complete history of order #123"
- Compliance requirements (SOX, GDPR, etc.)
- Rebuilding read models

### Use Together

```csharp
builder.Services
    .AddEventPublisher()
    // Operational telemetry
    .AddDeliveryLog(log => log.UseEntityFramework(opts => 
        opts.UseSqlServer(connectionString)))
    // Business audit trail
    .AddAuditTrail(audit => audit.UseNDJson(options =>
        options.DirectoryPath = "/secure/audit"));
```

**Scenario:**
1. `OrderPlaced` event is published
2. **Audit Trail** records the event payload for compliance
3. **Delivery Log** records that delivery took 123ms and succeeded
4. Compliance officer queries Audit Trail: "Show all orders for customer X"
5. Operations engineer queries Delivery Log: "What's our delivery success rate?"

---

## Delivery Log vs Error Handling

### Delivery Log

**What it does:**
- Records delivery outcomes as structured data
- Persists to storage (file, database)
- Enables historical analysis

**Best for:**
- Long-term tracking
- Querying and reporting
- SLA computation

### Error Handling

**What it does:**
- Intercepts errors in the publish pipeline
- Executes custom callbacks (logging, circuit-breaker, alerts)
- In-memory, immediate response

**Best for:**
- Real-time error handling
- Custom error policies
- Integration with alerting systems

### Use Together

```csharp
builder.Services
    .AddEventPublisher(options => options.ThrowOnErrors = false)
    .AddDeliveryLog(log => log
        .UseInMemory()
        .UseErrorHandler((context) =>
        {
            // Immediate alert on failure
            _alertingService.SendAlert($"Publish failed: {context.Exception.Message}");
        }));
```

---

## Decision Tree

```
Do you need to track delivery attempts?
│
├─ Yes, for monitoring/SLAs → Delivery Log
│
├─ Yes, to recover failed events → Dead-Letter
│
├─ Yes, for compliance/auditing → Audit Trail
│
└─ Yes, for real-time error handling → Error Handling

Most production systems use:
- Delivery Log (operational visibility)
- Dead-Letter (failure recovery)
- Audit Trail (compliance)
```

---

## Related Pages

- [Delivery Log Overview](README.md)
- [Dead-Letter Handling](../dead-letter/README.md)
- [Audit Trail](../audit-trail/README.md)
- [Error Handling](../../error-handling.md)
