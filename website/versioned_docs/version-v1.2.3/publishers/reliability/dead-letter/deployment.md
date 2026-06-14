# Dead-Letter Deployment

The dead-letter feature can be deployed in two topologies: same-process (relay runs inside the publisher application) or cross-process (dedicated worker process).

## Same-Process Deployment

Register all components in the same application host:

```csharp
services.AddEventPublisher(options =>
    {
        options.Source = new Uri("https://orders.example.com");
        options.ThrowOnErrors = false;
    })
    .AddRabbitMq(options => { /* transport config */ })
    .AddDeadLetter()
    .WithEntityFramework(options => options.UseSqlServer(connectionString))
    .WithReplayWorker(options =>
    {
        options.Interval = TimeSpan.FromSeconds(30);
        options.MaxRetryCount = 3;
    });
```

### Pros

| Benefit | Description |
|---------|-------------|
| **Simple setup** | One deployment, one configuration |
| **No additional infrastructure** | No separate worker process to manage |
| **Good for development** | Easy to test and debug |

### Cons

| Drawback | Description |
|----------|-------------|
| **Resource contention** | Relay competes with main application for CPU/memory |
| **Coupled lifecycle** | Relay restarts when application restarts |
| **Cannot scale independently** | Must scale entire application to scale replay |

### When to Use

- Development and testing
- Low to medium message volumes
- Applications where simplicity is paramount

---

## Cross-Process Deployment

Split the feature across two applications:

### Publisher Application

Publishes to the real transport and persists failures into the dead-letter store:

```csharp
// Publisher application Program.cs
services.AddEventPublisher(options =>
    {
        options.Source = new Uri("https://orders.example.com");
        options.ThrowOnErrors = false;
    })
    .AddRabbitMq(options => { /* transport config */ })
    .AddDeadLetter()
    .WithEntityFramework(options => options.UseSqlServer(connectionString));
    // No WithReplayWorker() here
```

### Worker Application

Connects to the same store and runs `WithReplayWorker()`:

```csharp
// Worker application Program.cs
services.AddEventPublisher("transport", builder => builder
    .Configure(options =>
    {
        options.Source = new Uri("https://orders.example.com/transport");
        options.ThrowOnErrors = true;
    })
    .AddRabbitMq(options => { /* transport config */ }));

services.AddEventPublisher()
    .AddDeadLetter()
    .UseRepository<DbDeadLetterMessage, EntityDeadLetterMessageStore<DbDeadLetterMessage>>()
    .WithFactory<DbDeadLetterMessage, DefaultDeadLetterMessageFactory<DbDeadLetterMessage>>()
    .WithReplayWorker(options =>
    {
        options.TransportPublisherName = "transport";
        options.Interval = TimeSpan.FromSeconds(30);
    });
```

### Pros

| Benefit | Description |
|---------|-------------|
| **Independent scaling** | Scale workers without scaling the publisher |
| **Isolated failures** | Worker crashes don't affect the publisher |
| **Dedicated resources** | Worker can run on different hardware |
| **Simplified deployment** | Update worker logic without redeploying publisher |

### Cons

| Drawback | Description |
|----------|-------------|
| **More complex deployment** | Separate worker process to manage |
| **Shared database** | Requires database access from both applications |
| **Additional infrastructure** | Need to monitor and maintain worker |

### When to Use

- High message volumes
- Mission-critical applications requiring high availability
- When replay processing is resource-intensive
- When you need to scale replay independently

---

## Comparison Table

| Aspect | Same-Process | Cross-Process |
|--------|-------------|---------------|
| **Setup complexity** | Simple | Moderate |
| **Infrastructure** | Minimal | Additional worker process |
| **Scalability** | Limited | Excellent |
| **Isolation** | None | Full |
| **Resource contention** | Yes | No |
| **Deployment** | Single | Multiple |
| **Best for** | Dev/Test, low volume | Production, high volume |

---

## Related Pages

- [Dead-Letter Overview](README.md)
- [Background Worker](background-worker.md)
- [EF Core Integration](ef-core-integration.md)
