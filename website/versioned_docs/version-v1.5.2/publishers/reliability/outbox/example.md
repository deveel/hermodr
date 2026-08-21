# Outbox End-to-End Example

This walkthrough shows a minimal order service that atomically writes an order row and an outbox record, then relies on the in-process relay to forward the event to RabbitMQ.

## Scenario

An `OrderService` needs to:
1. Save an order to the database
2. Publish an `OrderPlaced` event
3. Ensure the event is never lost, even if RabbitMQ is down

The outbox pattern solves this by writing the event to the database first, then letting a background relay handle delivery.

---

## Step 1: Define the Event

Annotate the event data class with `[Event]` so the framework can generate the correct CloudEvents attributes:

```csharp
using Hermodr;

[Event("order.placed", "1.0")]
public class OrderPlaced
{
    public Guid    OrderId    { get; set; }
    public string  CustomerId { get; set; } = default!;
    public decimal Total      { get; set; }
}
```

---

## Step 2: Create the Outbox Entity

`DbOutboxMessage` can be used **directly** — no subclass is required unless you need extra columns. For this example, we add a `TenantId` routing column:

```csharp
using Hermodr.Publisher.Outbox.EntityFramework;

// Subclass only needed because we add an extra column.
// Without TenantId we could use DbOutboxMessage directly.
public class OrderOutboxMessage : DbOutboxMessage
{
    public string? TenantId { get; set; }
}
```

---

## Step 3: Create the Message Factory

Use `PopulateFromCloudEvent` to map the `CloudEvent` to the entity:

```csharp
using Hermodr.Publisher.Outbox.EntityFramework;

public class OrderOutboxMessageFactory : IOutboxMessageFactory<OrderOutboxMessage>
{
    public OrderOutboxMessage Create(CloudEvent cloudEvent, OutboxPublishOptions? options = null)
    {
        var message = new OrderOutboxMessage();
        message.PopulateFromCloudEvent(cloudEvent);
        message.TenantId = cloudEvent["tenantid"]?.ToString();
        return message;
    }
}
```

---

## Step 4: Create the Application DbContext

Derive from `OutboxDbContext` to inherit the outbox entity configuration:

```csharp
using Hermodr.Publisher.Outbox.EntityFramework;
using Microsoft.EntityFrameworkCore;

public class AppDbContext : OutboxDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Order> Orders { get; set; } = null!;
}

public class Order
{
    public Guid Id { get; set; }
    public string CustomerId { get; set; } = default!;
    public decimal Total { get; set; }
}
```

---

## Step 5: Register Services in Program.cs

Wire all components in the DI container:

```csharp
var builder = WebApplication.CreateBuilder(args);

// Register the application DbContext
builder.Services.AddDbContext<AppDbContext>(opts =>
    opts.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

// Register event publisher with outbox
builder.Services
    .AddEventPublisher(opts => opts.Source = new Uri("https://orders.example.com"))
    .AddOutbox<OrderOutboxMessage>()
    .WithEntityFramework()  // uses the AppDbContext registered above
    .WithFactory<OrderOutboxMessageFactory>()
    .WithRelay(relay =>
    {
        relay.Interval     = TimeSpan.FromSeconds(15);
        relay.MaxBatchSize = 50;
    })
    .AddRabbitMq(opts =>
    {
        opts.ConnectionString = builder.Configuration["RabbitMq:ConnectionString"]!;
        opts.ExchangeName     = "events";
    });

var app = builder.Build();

// Create database (for development; use migrations in production)
using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
}

app.MapPost("/api/orders", async (OrderService service, PlaceOrderCommand cmd) =>
{
    await service.PlaceOrderAsync(cmd);
    return Results.Ok();
});

app.Run();
```

---

## Step 6: Implement the Order Service

Inject `IEventPublisher` and call `PublishAsync` as usual. No special outbox API is required:

```csharp
public class OrderService
{
    private readonly AppDbContext    _db;
    private readonly IEventPublisher _publisher;

    public OrderService(AppDbContext db, IEventPublisher publisher)
    {
        _db        = db;
        _publisher = publisher;
    }

    public async Task PlaceOrderAsync(PlaceOrderCommand cmd, CancellationToken ct = default)
    {
        var order = new Order { CustomerId = cmd.CustomerId, Total = cmd.Total };
        await _db.Orders.AddAsync(order, ct);

        // Writes only to the outbox table; the relay will forward to RabbitMQ.
        await _publisher.PublishAsync(new OrderPlaced
        {
            OrderId    = order.Id,
            CustomerId = order.CustomerId,
            Total      = order.Total,
        }, cancellationToken: ct);

        // Commits both the order row and the outbox record atomically
        await _db.SaveChangesAsync(ct);
    }
}

public record PlaceOrderCommand(string CustomerId, decimal Total);
```

**Key Point:** `SaveChangesAsync` is called **once** after both the domain write and the publish call, ensuring that the two rows commit atomically.

---

## Step 7: Test the Flow

### Test Case 1: Normal Operation

1. POST `/api/orders` with `{ "customerId": "cust-123", "total": 99.95 }`
2. Order is saved to `Orders` table
3. Outbox record is created with `Status = Pending`
4. Transaction commits
5. Relay wakes up (within 15 seconds), picks up the pending message
6. Event is published to RabbitMQ
7. Outbox record is marked as `Delivered`

### Test Case 2: Broker Unavailable

1. Stop RabbitMQ
2. POST `/api/orders` with `{ "customerId": "cust-123", "total": 99.95 }`
3. Order is saved, outbox record is created — **succeeds**
4. Relay wakes up, attempts to publish to RabbitMQ — **fails**
5. Outbox record is updated: `RetryCount = 1`, `NextRetryAt = now + 30s`
6. Relay wakes up again after 30 seconds, retries — **fails again**
7. Process repeats until retry limit is reached or RabbitMQ comes back online

**Result:** The event is never lost — it sits in the outbox table waiting for the broker to become available.

### Test Case 3: Application Restart

1. POST `/api/orders` — outbox record created
2. Stop the application **before** relay delivers the event
3. Restart the application
4. Relay finds the pending outbox record and delivers it

**Result:** Events survive application restarts because they're persisted in the database.

---

## Complete Code Listing

### Program.cs

```csharp
using Hermodr;
using Hermodr.Publisher.Outbox.EntityFramework;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(opts =>
    opts.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services
    .AddEventPublisher(opts => opts.Source = new Uri("https://orders.example.com"))
    .AddOutbox<OrderOutboxMessage>()
    .WithEntityFramework()
    .WithFactory<OrderOutboxMessageFactory>()
    .WithRelay(relay =>
    {
        relay.Interval     = TimeSpan.FromSeconds(15);
        relay.MaxBatchSize = 50;
    })
    .AddRabbitMq(opts =>
    {
        opts.ConnectionString = builder.Configuration["RabbitMq:ConnectionString"]!;
        opts.ExchangeName     = "events";
    });

var app = builder.Build();

// Create database (for development)
using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
}

app.MapPost("/api/orders", async (OrderService service, PlaceOrderCommand cmd) =>
{
    await service.PlaceOrderAsync(cmd);
    return Results.Ok();
});

app.Run();
```

### OrderService.cs

```csharp
using CloudNative.CloudEvents;
using Hermodr;
using Microsoft.EntityFrameworkCore;

public class OrderService
{
    private readonly AppDbContext    _db;
    private readonly IEventPublisher _publisher;

    public OrderService(AppDbContext db, IEventPublisher publisher)
    {
        _db        = db;
        _publisher = publisher;
    }

    public async Task PlaceOrderAsync(PlaceOrderCommand cmd, CancellationToken ct = default)
    {
        var order = new Order { CustomerId = cmd.CustomerId, Total = cmd.Total };
        await _db.Orders.AddAsync(order, ct);

        await _publisher.PublishAsync(new OrderPlaced
        {
            OrderId    = order.Id,
            CustomerId = order.CustomerId,
            Total      = order.Total,
        }, cancellationToken: ct);

        await _db.SaveChangesAsync(ct);
    }
}

public class Order
{
    public Guid Id { get; set; }
    public string CustomerId { get; set; } = default!;
    public decimal Total { get; set; }
}

public record PlaceOrderCommand(string CustomerId, decimal Total);

[Event("order.placed", "1.0")]
public class OrderPlaced
{
    public Guid    OrderId    { get; set; }
    public string  CustomerId { get; set; } = default!;
    public decimal Total      { get; set; }
}
```

### OutboxEntity.cs

```csharp
using Hermodr.Publisher.Outbox.EntityFramework;

public class OrderOutboxMessage : DbOutboxMessage
{
    public string? TenantId { get; set; }
}
```

### MessageFactory.cs

```csharp
using CloudNative.CloudEvents;
using Hermodr.Publisher.Outbox;
using Hermodr.Publisher.Outbox.EntityFramework;

public class OrderOutboxMessageFactory : IOutboxMessageFactory<OrderOutboxMessage>
{
    public OrderOutboxMessage Create(CloudEvent cloudEvent, OutboxPublishOptions? options = null)
    {
        var message = new OrderOutboxMessage();
        message.PopulateFromCloudEvent(cloudEvent);
        message.TenantId = cloudEvent["tenantid"]?.ToString();
        return message;
    }
}
```

---

## Next Steps

- **[Relay Service](relay.md)** — Configure relay polling interval and batch size
- **[Lifecycle](lifecycle.md)** — Understand message states and transitions
- **[Dead-Letter Handling](../dead-letter/README.md)** — Capture and retry failed deliveries
- **[Delivery Log](../delivery-log/README.md)** — Track delivery attempts and latency

## Related Pages

- [Transactional Outbox Overview](README.md)
- [EF Core Integration](ef-core-integration.md)
- [Registration](registration.md)
