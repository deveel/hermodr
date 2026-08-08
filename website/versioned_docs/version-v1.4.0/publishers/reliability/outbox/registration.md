# Outbox Registration

Once your components are ready, wire them up in your application using the fluent `EventPublisherBuilder` chain.

## Registration Patterns

### Minimal Setup (Custom Store)

For non-EF Core backends or custom implementations:

```csharp
builder.Services
    .AddEventPublisher()
    .AddOutbox<OrderOutboxMessage>()
    .WithStore<MyOutboxMessageStore>()
    .WithFactory<OrderOutboxMessageFactory>();
```

### Minimal Setup (EF Core Integration)

Use `DbOutboxMessage` directly when no extra columns are needed:

```csharp
builder.Services
    .AddEventPublisher()
    .AddOutbox<DbOutboxMessage>()   // use the built-in entity as-is
    .WithEntityFramework()
    .WithFactory<DbOutboxMessageFactory>();
```

Or with a subclass when you need application-specific columns:

```csharp
builder.Services
    .AddEventPublisher()
    .AddOutbox<OrderOutboxMessage>()  // OrderOutboxMessage : DbOutboxMessage
    .WithEntityFramework()
    .WithFactory<OrderOutboxMessageFactory>();
```

### Convenience Method

The `AddEntityFrameworkOutbox` method combines outbox and EF registration:

```csharp
builder.Services
    .AddEventPublisher()
    .AddEntityFrameworkOutbox(opts => opts.UseSqlServer(connectionString))
    .WithFactory<OrderOutboxMessageFactory>();
```

---

## With Inline Options

Configure outbox options inline during registration:

```csharp
builder.Services
    .AddEventPublisher()
    .AddOutbox<OrderOutboxMessage>(options =>
    {
        options.ChannelName = "outbox";
    })
    .WithStore<MyOutboxMessageStore>()
    .WithFactory<OrderOutboxMessageFactory>();
```

### OutboxPublishOptions

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ChannelName` | `string` | `null` | Logical name for the outbox channel |

> **Note:** `OutboxPublishOptions` inherits from `EventPublishOptions` and currently adds no outbox-specific properties. It exists as a dedicated type so that future releases can introduce outbox-only settings without a breaking change.

---

## Bound from Configuration

Register options from `appsettings.json`:

```csharp
builder.Services
    .AddEventPublisher()
    .AddOutbox<OrderOutboxMessage>("Events:Outbox")
    .WithStore<MyOutboxMessageStore>()
    .WithFactory<OrderOutboxMessageFactory>();
```

### Configuration File

```json
{
  "Events": {
    "Outbox": {
      "ChannelName": "outbox"
    }
  }
}
```

---

## Complete Registration with Relay and Transport

### Same-Process Deployment

When the relay runs inside the same application host:

```csharp
builder.Services
    .AddEventPublisher()
    .AddOutbox<OrderOutboxMessage>()
    .WithEntityFramework(opts => opts.UseSqlServer(connectionString))
    .WithFactory<OrderOutboxMessageFactory>()
    .WithRelay(relay =>
    {
        relay.Interval     = TimeSpan.FromSeconds(15);
        relay.MaxBatchSize = 100;
    })
    .AddRabbitMq(opts =>
    {
        opts.ConnectionString = "amqp://guest:guest@localhost:5672";
        opts.ExchangeName     = "events";
    });
```

> **Important:** Calling `.WithRelay(…)` automatically sets `EventPublisherOptions.ThrowOnErrors = true` via a post-configuration callback. This ensures transport errors surface to the relay service so it can correctly mark messages as `Failed`.

### Configuration-Bound Equivalent

```csharp
builder.Services
    .AddEventPublisher()
    .AddOutbox<OrderOutboxMessage>("Events:Outbox")
    .WithEntityFramework(opts => opts.UseSqlServer(connectionString))
    .WithFactory<OrderOutboxMessageFactory>()
    .WithRelay("Events:OutboxRelay")
    .AddRabbitMq("Events:RabbitMq");
```

```json
{
  "Events": {
    "Outbox": {
      "ChannelName": "outbox"
    },
    "OutboxRelay": { 
      "Interval": "00:00:15", 
      "MaxBatchSize": 100 
    },
    "RabbitMq": { 
      "ConnectionString": "amqp://...", 
      "ExchangeName": "events" 
    }
  }
}
```

---

## Cross-Process Deployment

For larger deployments, run the relay as a **dedicated worker process**:

### Main Application (Publisher Only)

Registers only the outbox channel — no relay, no transport:

```csharp
// Main application Program.cs
builder.Services
    .AddEventPublisher()
    .AddOutbox<OrderOutboxMessage>()
    .WithEntityFramework(opts => 
        opts.UseSqlServer(builder.Configuration.GetConnectionString("Shared")));
```

### Relay Worker (Separate Process)

Registers repository, factory, relay, and transport — but not the outbox channel:

```csharp
// Relay worker Program.cs
builder.Services
    .AddEventPublisher()
    .AddOutbox<OrderOutboxMessage>()
    .WithEntityFramework(opts =>
        opts.UseSqlServer(builder.Configuration.GetConnectionString("Shared")))
    .WithFactory<OrderOutboxMessageFactory>()
    .WithRelay(relay =>
    {
        relay.Interval     = TimeSpan.FromSeconds(10);
        relay.MaxBatchSize = 200;
    })
    .AddRabbitMq(opts =>
    {
        opts.ConnectionString = "amqp://guest:guest@rabbitmq:5672";
        opts.ExchangeName     = "events";
    });
```

### Benefits of Cross-Process Deployment

| Benefit | Description |
|---------|-------------|
| **Independent scaling** | Scale relay workers without scaling the main application |
| **Isolated failures** | Relay crashes don't affect the main application |
| **Dedicated resources** | Relay can run on different hardware/infrastructure |
| **Simplified deployment** | Update relay logic without redeploying the main application |

---

## Combining Outbox with Other Channels

The outbox channel participates in the same fan-out as any other channel. You can mix the outbox with direct transport channels:

```csharp
builder.Services
    .AddEventPublisher()
    // Guaranteed, async delivery for all events via outbox
    .AddOutbox<OrderOutboxMessage>()
        .WithEntityFramework(opts => opts.UseSqlServer(connectionString))
        .WithFactory<OrderOutboxMessageFactory>()
        .WithRelay(r => r.Interval = TimeSpan.FromSeconds(20))
    // Direct delivery for high-priority events
    .AddWebhooks(opts =>
    {
        opts.ChannelName   = "priority-webhook";
        opts.EndpointUrl   = "https://partner.example.com/events";
        opts.SigningSecret = "s3cr3t";
    });
```

Use [Named Channels](../../named-channels.md) to route specific event types to specific channels.

---

## Related Pages

- [Transactional Outbox Overview](README.md)
- [EF Core Integration](ef-core-integration.md)
- [Relay Service](relay.md)
