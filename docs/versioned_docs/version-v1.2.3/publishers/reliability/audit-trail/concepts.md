# Audit Trail Concepts

The audit trail is often confused with the delivery log and event store. Understanding the differences helps you use each feature correctly.

## What Is an Audit Trail?

An **audit trail** is an immutable, chronological record of all events that occurred in your system. It serves compliance, auditing, and historical analysis purposes.

### Key Characteristics

| Characteristic | Description |
|----------------|-------------|
| **Immutable** | Once written, entries cannot be modified |
| **Append-only** | New entries are added, never updated |
| **Chronological** | Entries are ordered by event time |
| **Complete** | Records all events, regardless of delivery outcome |
| **Long-term** | Designed for retention periods (months to years) |

## Audit Trail vs Delivery Log vs Event Store

These three features serve different purposes:

### Audit Trail

**Purpose:** Compliance, auditing, historical record

**What it stores:**
- Event payload (the `CloudEvent` with all attributes)
- When the event occurred (`CloudEvent.time`)
- When it was recorded (`storedAt`)

**Query patterns:**
- "Show all events for order #123"
- "What events occurred between 2pm and 3pm?"
- "Prove that event X happened at time T"

**Retention:** Long-term (months to years, per compliance requirements)

### Delivery Log

**Purpose:** Operational monitoring, troubleshooting

**What it stores:**
- Publish attempt metadata
- Which channel was used
- How many attempts were made
- Success/failure outcome
- Error details (if failed)
- Elapsed time per attempt

**Query patterns:**
- "What's the delivery success rate?"
- "Which channel is failing most often?"
- "How long does delivery typically take?"

**Retention:** Short-term (days to weeks, for operational analysis)

### Event Store (Event Sourcing)

**Purpose:** Domain state reconstruction, read models

**What it stores:**
- Domain events as the source of truth
- Aggregate version numbers
- Optimized for replaying to rebuild state

**Query patterns:**
- "Rebuild the Order aggregate from scratch"
- "Get all events for aggregate X up to version N"

**Retention:** Indefinite (as long as the domain model exists)

---

## Comparison Table

| Aspect | Audit Trail | Delivery Log | Event Store |
|--------|-------------|--------------|-------------|
| **Primary purpose** | Compliance, auditing | Operational monitoring | State reconstruction |
| **Records** | Events (payloads) | Delivery attempts | Domain events |
| **Immutability** | Strict (append-only) | Moderate (status updates) | Strict (append-only) |
| **One record per** | Event | Attempt | Event |
| **Retention** | Long-term (years) | Short-term (days/weeks) | Indefinite |
| **Query focus** | Historical analysis | Health monitoring | State replay |
| **Compliance-ready** | ✅ Yes | ❌ No | ⚠️ Partial |

---

## When to Use Each

### Use Audit Trail When

- ✅ You need to prove events occurred for regulatory compliance (SOX, GDPR, HIPAA)
- ✅ You want a complete historical record of all events
- ✅ You need to answer "what happened?" questions
- ✅ You want to track events independent of delivery success
- ✅ You need long-term retention (years)

### Use Delivery Log When

- ✅ You need to monitor delivery health in real-time
- ✅ You want to compute SLAs (success rates, latency percentiles)
- ✅ You need to troubleshoot delivery failures
- ✅ You want to identify problematic channels or event types
- ✅ You need short-term operational visibility

### Use Event Store When

- ✅ You're practicing event sourcing
- ✅ You need to rebuild read models from scratch
- ✅ You want to track aggregate state changes over time
- ✅ You need optimistic concurrency control
- ✅ Your domain model is event-driven

---

## Using Them Together

Most production systems benefit from using multiple features:

### Compliance + Operations

```csharp
builder.Services
    .AddEventPublisher()
    // Compliance: immutable event record
    .AddAuditTrail(audit => audit.UseNDJson(options =>
    {
        options.DirectoryPath = "/secure/audit";
        options.MaxFileCount = 365;  // Keep 1 year
    }))
    // Operations: delivery health monitoring
    .AddDeliveryLog(log => log.UseEntityFramework(opts =>
        opts.UseSqlServer(connectionString)));
```

**Scenario:**
1. `PaymentProcessed` event is published
2. **Audit Trail** records the event for compliance
3. **Delivery Log** tracks that delivery took 45ms and succeeded
4. Compliance officer queries Audit Trail for regulatory report
5. Operations engineer monitors Delivery Log dashboard for issues

### Full Reliability Stack

```csharp
builder.Services
    .AddEventPublisher()
    // Durability: outbox pattern
    .AddEntityFrameworkOutbox(opts => opts.UseSqlServer(connectionString))
    .WithRelay()
    // Compliance: audit trail
    .AddAuditTrail(audit => audit.UseNDJson(options =>
        options.DirectoryPath = "/secure/audit"))
    // Operations: delivery monitoring
    .AddDeliveryLog(log => log.UseEntityFramework(opts =>
        opts.UseSqlServer(connectionString)))
    // Recovery: dead-letter handling
    .AddDeadLetter()
    .WithEntityFramework(opts => opts.UseSqlServer(connectionString))
    .WithReplayWorker();
```

---

## Compliance Considerations

### Regulatory Requirements

Different regulations have different audit trail requirements:

| Regulation | Typical Requirements |
|------------|----------------------|
| **SOX** | Financial events, 7+ years retention, tamper-proof |
| **GDPR** | Personal data processing events, right to erasure |
| **HIPAA** | Healthcare events, 6+ years, access controls |
| **PCI-DSS** | Payment events, 1+ year, encryption at rest |

### Best Practices

1. **Immutable storage** — Use write-once, read-many (WORM) storage when possible
2. **Encryption** — Encrypt audit records at rest and in transit
3. **Access controls** — Restrict who can read audit records
4. **Retention policies** — Define and enforce retention periods
5. **Backup and archival** — Regular backups, cold storage for old records
6. **Audit the audit trail** — Log access to audit records

---

## Related Pages

- [Audit Trail Overview](README.md)
- [NDJSON Backend](ndjson-backend.md)
- [Delivery Log Comparison](../delivery-log/comparison.md)
