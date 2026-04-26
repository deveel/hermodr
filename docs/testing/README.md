# Test Publisher

The `Deveel.Events.TestPublisher` package provides an in-memory publish channel (`TestEventPublishChannel`) that can be used in unit and integration tests to assert on published events without requiring a real messaging transport.

## Installation

```bash
dotnet add package Deveel.Events.TestPublisher
```

## Registration

Register a test channel using one of the `AddTestChannel` overloads on `EventPublisherBuilder`.

### With a `Func<CloudEvent, Task>` callback

```csharp
using CloudNative.CloudEvents;
using Deveel.Events;
using Microsoft.Extensions.DependencyInjection;

var publishedEvents = new List<CloudEvent>();

var services = new ServiceCollection();
services.AddEventPublisher()
        .AddTestChannel(async @event =>
        {
            publishedEvents.Add(@event);
            await Task.CompletedTask;
        });

var provider = services.BuildServiceProvider();
```

### With an `Action<CloudEvent>` callback (synchronous)

```csharp
var publishedEvents = new List<CloudEvent>();

services.AddEventPublisher()
        .AddTestChannel(@event => publishedEvents.Add(@event));
```

### With a custom `IEventPublishCallback`

```csharp
public class MyTestCallback : IEventPublishCallback
{
    public List<CloudEvent> Events { get; } = new();

    public Task OnEventPublishedAsync(CloudEvent @event)
    {
        Events.Add(@event);
        return Task.CompletedTask;
    }
}

var callback = new MyTestCallback();

services.AddEventPublisher()
        .AddTestChannel(callback);
```

## Usage in xUnit

```csharp
using CloudNative.CloudEvents;
using Deveel.Events;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public class OrderServiceTests
{
    private readonly IEventPublisher _publisher;
    private readonly List<CloudEvent> _published = new();

    public OrderServiceTests()
    {
        var services = new ServiceCollection();
        services.AddEventPublisher()
                .AddTestChannel(@event => _published.Add(@event));
        services.AddTransient<OrderService>();

        var provider = services.BuildServiceProvider();
        _publisher = provider.GetRequiredService<IEventPublisher>();
    }

    [Fact]
    public async Task PlaceOrder_PublishesOrderPlacedEvent()
    {
        var service = new OrderService(_publisher);

        await service.PlaceOrderAsync(Guid.NewGuid(), 99.95m, "USD");

        Assert.Single(_published);
        var @event = _published[0];
        Assert.Equal("order.placed", @event.Type);
        Assert.Equal("USD", ((OrderPlacedData)@event.Data!).Currency);
    }
}
```

## `IEventPublishCallback`

```csharp
public interface IEventPublishCallback
{
    Task OnEventPublishedAsync(CloudEvent @event);
}
```

Implement this interface when you need richer callback logic (e.g. recording timestamps, simulating failures, asserting within the callback).

## Combining with `IEventSystemTime`

Replace the system time to control event timestamps in tests:

```csharp
services.AddEventPublisher()
        .UseSystemTime<FrozenSystemTime>()
        .AddTestChannel(@event => _published.Add(@event));
```

```csharp
public class FrozenSystemTime : IEventSystemTime
{
    public DateTimeOffset UtcNow { get; } = new DateTimeOffset(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);
}
```

## Related pages

- [Event Publisher](../concepts/event-publisher.md)
- [Publish Channels](../concepts/publish-channels.md)

