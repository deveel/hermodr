# Outbox EF Core Integration

The `Hermodr.Publisher.Outbox.EntityFramework` package eliminates most boilerplate by providing ready-made EF Core implementations.

## What's Included

| Type | Role |
|------|------|
| `DbOutboxMessage` | Complete `IOutboxMessage` implementation with EF mappings |
| `EntityOutboxMessageRepository<TMessage>` | EF Core repository implementation |
| `OutboxDbContext` | Minimal `DbContext` with outbox configured |
| `WithEntityFramework()` | Fluent extension that wires all EF components |
| `AddEntityFrameworkOutbox()` | Convenience method combining outbox + EF registration |

---

## DbOutboxMessage

`DbOutboxMessage` is a **complete, concrete implementation of `IOutboxMessage`** — it can be used directly as your outbox entity without writing any additional code.

### Features

- Maps well-known CloudEvents context attributes as scalar columns
- Stores data payload in `DataText` (string/JSON) or `DataBytes` (binary)
- Extension attributes stored as child rows in `CloudEventAttributes` table
- Full `IOutboxMessage` implementation provided

### Column Mapping

| Column | Type | Purpose |
|--------|------|---------|
| `Id` | `string` | Primary key (GUID without hyphens) |
| `EventType` | `string` | CloudEvents `type` attribute |
| `Source` | `string` | CloudEvents `source` attribute |
| `Subject` | `string?` | CloudEvents `subject` attribute |
| `EventTime` | `DateTimeOffset` | CloudEvents `time` attribute |
| `DataContentType` | `string?` | CloudEvents `datacontenttype` |
| `DataSchema` | `string?` | CloudEvents `dataschema` |
| `DataText` | `string?` | JSON payload (for JSON content types) |
| `DataBytes` | `byte[]?` | Binary payload (for non-JSON content types) |
| `Status` | `OutboxMessageStatus` | Delivery lifecycle status |
| `ErrorMessage` | `string?` | Last failure reason |
| `RetryCount` | `int` | Number of delivery attempts |
| `NextRetryAt` | `DateTimeOffset?` | Scheduled retry time |
| `CreatedAt` | `DateTimeOffset` | When the record was created |
| `LastStatusAt` | `DateTimeOffset` | When status last changed |

### Using DbOutboxMessage Directly

When you don't need any application-specific columns, register `DbOutboxMessage` itself:

```csharp
builder.Services
    .AddEventPublisher()
    .AddOutbox<DbOutboxMessage>()   // no subclass needed
    .WithEntityFramework(opts => opts.UseSqlServer(connectionString))
    .WithFactory<DbOutboxMessageFactory>()
    .WithRelay();
```

### Subclassing for Application-Specific Columns

Derive from `DbOutboxMessage` only when you need to add columns:

```csharp
using Hermodr.Publisher.Outbox.EntityFramework;

public class OrderOutboxMessage : DbOutboxMessage
{
    public string? TenantId { get; set; }
    public string? OrderId { get; set; }  // For indexing/querying
}
```

> **Constraint:** `WithEntityFramework()` requires that the message entity type derives from `DbOutboxMessage`. It checks this at registration time and throws `InvalidOperationException` if the constraint is not satisfied.

---

## Message Factory with DbOutboxMessage

Use the `PopulateFromCloudEvent` helper inside your factory:

### Factory for DbOutboxMessage (No Subclass)

```csharp
using Hermodr.Publisher.Outbox.EntityFramework;

public class DbOutboxMessageFactory : IOutboxMessageFactory<DbOutboxMessage>
{
    public DbOutboxMessage Create(CloudEvent cloudEvent, OutboxPublishOptions? options = null)
    {
        var message = new DbOutboxMessage();
        message.PopulateFromCloudEvent(cloudEvent);
        return message;
    }
}
```

### Factory for a Subclass with Extra Columns

```csharp
public class OrderOutboxMessageFactory : IOutboxMessageFactory<OrderOutboxMessage>
{
    public OrderOutboxMessage Create(CloudEvent cloudEvent, OutboxPublishOptions? options = null)
    {
        var message = new OrderOutboxMessage();
        message.PopulateFromCloudEvent(cloudEvent);  // populates all standard columns
        message.TenantId = /* resolve from context */;
        message.OrderId = cloudEvent["orderid"]?.ToString();
        return message;
    }
}
```

`PopulateFromCloudEvent` maps all standard attributes and extension attributes from the live `CloudEvent` to the entity columns and child rows.

---

## OutboxDbContext

`OutboxDbContext` is a minimal `DbContext` that applies the built-in EF model configuration automatically:

```csharp
using Hermodr.Publisher.Outbox.EntityFramework;
using Microsoft.EntityFrameworkCore;

public class OutboxDbContext : DbContext
{
    public OutboxDbContext(DbContextOptions<OutboxDbContext> options) : base(options) { }

    public DbSet<DbOutboxMessage> OutboxMessages { get; set; } = null!;
    public DbSet<DbCloudEventAttribute> CloudEventAttributes { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Apply built-in configurations
        modelBuilder.ApplyConfiguration(new DbOutboxMessageConfiguration());
        modelBuilder.ApplyConfiguration(new DbCloudEventAttributeConfiguration());
    }
}
```

### Deriving from OutboxDbContext

When you need to add your own `DbSet` properties:

```csharp
public class AppDbContext : OutboxDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Order> Orders { get; set; } = null!;
}
```

> **Note:** Pass the derived context's own `DbContextOptions<AppDbContext>` through the constructor chain to the protected `OutboxDbContext(DbContextOptions)` overload.

---

## Registration

### Minimal Setup (Using DbOutboxMessage Directly)

```csharp
builder.Services.AddDbContext<OutboxDbContext>(opts =>
    opts.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services
    .AddEventPublisher()
    .AddOutbox<DbOutboxMessage>()
    .WithEntityFramework()
    .WithFactory<DbOutboxMessageFactory>()
    .WithRelay();
```

### With Subclass for Extra Columns

```csharp
builder.Services.AddDbContext<AppDbContext>(opts =>
    opts.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services
    .AddEventPublisher()
    .AddOutbox<OrderOutboxMessage>()  // OrderOutboxMessage : DbOutboxMessage
    .WithEntityFramework()
    .WithFactory<OrderOutboxMessageFactory>()
    .WithRelay(relay =>
    {
        relay.Interval     = TimeSpan.FromSeconds(15);
        relay.MaxBatchSize = 100;
    });
```

### Convenience Method: AddEntityFrameworkOutbox

An `AddEntityFrameworkOutbox` method is provided for convenience, which combines the outbox and EF registration in one call:

```csharp
builder.Services
    .AddEventPublisher()
    .AddEntityFrameworkOutbox(opts => opts.UseSqlServer(connectionString))
    .WithRelay();
```

> **Note:** When you call `.WithEntityFramework()`, the framework automatically registers the `EntityOutboxMessageRepository<DbOutboxMessage>` implementation for you, so you do not need to call `.WithStore<…>()` separately. It also registers the `IOutboxMessageFactory` implementation that constructs `DbOutboxMessage` instances.

---

## Database Schema

When you run migrations or call `Database.EnsureCreated()`, the following tables are created:

### OutboxMessages Table

```sql
CREATE TABLE OutboxMessages (
    Id                 nvarchar(450) NOT NULL PRIMARY KEY,
    EventType          nvarchar(256) NOT NULL,
    Source             nvarchar(256) NOT NULL,
    Subject            nvarchar(256) NULL,
    EventTime          datetimeoffset NOT NULL,
    DataContentType    nvarchar(256) NULL,
    DataSchema         nvarchar(256) NULL,
    DataText           nvarchar(max) NULL,
    DataBytes          varbinary(max) NULL,
    Status             int NOT NULL DEFAULT 0,
    ErrorMessage       nvarchar(max) NULL,
    RetryCount         int NOT NULL DEFAULT 0,
    NextRetryAt        datetimeoffset NULL,
    CreatedAt          datetimeoffset NOT NULL,
    LastStatusAt       datetimeoffset NOT NULL
);
```

### CloudEventAttributes Table

```sql
CREATE TABLE CloudEventAttributes (
    Id               nvarchar(450) NOT NULL PRIMARY KEY,
    OutboxMessageId  nvarchar(450) NOT NULL,
    AttributeName    nvarchar(256) NOT NULL,
    AttributeValue   nvarchar(max) NULL,
    CONSTRAINT FK_CloudEventAttributes_OutboxMessages 
        FOREIGN KEY (OutboxMessageId) REFERENCES OutboxMessages(Id)
        ON DELETE CASCADE
);
```

---

## Related Pages

- [Transactional Outbox Overview](README.md)
- [Outbox Components](components.md)
- [Registration](registration.md)
