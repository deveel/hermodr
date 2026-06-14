# Dead-Letter EF Core Integration

The `Hermodr.Publisher.DeadLetter.EntityFramework` package provides a ready-made EF implementation.

## What's Included

| Type | Purpose |
|------|---------|
| `DbDeadLetterMessage` | Concrete dead-letter entity implementing `IDeadLetterMessage` |
| `DbDeadLetterAttribute` | Stores CloudEvents extension attributes |
| `DeadLetterDbContext` | Minimal EF `DbContext` for dead-letter storage |
| `EntityDeadLetterMessageStore<TMessage>` | EF-backed `IDeadLetterMessageStore` implementation |
| `DefaultDeadLetterMessageFactory<TMessage>` | Converts `DeadLetterContext` into `DbDeadLetterMessage` |

## Recommended Setup

```csharp
using Hermodr;
using Microsoft.EntityFrameworkCore;

services.AddEventPublisher(options =>
    {
        options.Source = new Uri("https://orders.example.com");
        options.ThrowOnErrors = false;
    })
    .AddRabbitMq(options =>
    {
        options.ConnectionString = "amqp://guest:guest@localhost:5672";
        options.ExchangeName = "orders";
    })
    .AddDeadLetter()
    .WithEntityFramework(options =>
        options.UseSqlite("Data Source=deadletters.db"))
    .WithReplayWorker(options =>
    {
        options.Interval = TimeSpan.FromSeconds(30);
        options.MaxRetryCount = 3;
        options.RetryInterval = TimeSpan.FromMinutes(1);
    });
```

## What DbDeadLetterMessage Stores

`DbDeadLetterMessage` maps the most important CloudEvents data as columns:

- `Id`, `EventType`, `Source`, `Subject`, `EventTime`
- `DataContentType`, `DataSchema`
- Event payload in `DataText` or `DataBytes`
- Replay metadata: `Status`, `ReplayCount`, `NextReplayAt`, `CreatedAt`, `LastStatusAt`
- Channel and publisher metadata: `PublisherName`, `ChannelName`, `ChannelType`

CloudEvents extension attributes are stored in child rows through `DbDeadLetterAttribute`.

## Creating the Database

The package provides model configuration through `DeadLetterDbContext`. Create the database the same way you would for any other EF Core context:

```csharp
await using var scope = host.Services.CreateAsyncScope();
var db = scope.ServiceProvider.GetRequiredService<DeadLetterDbContext>();
await db.Database.EnsureCreatedAsync();
```

Or use migrations if you want the dead-letter tables to be managed as part of your normal schema lifecycle.

## Related Pages

- [Dead-Letter Overview](README.md)
- [Persistence](persistence.md)
- [Deployment](deployment.md)
