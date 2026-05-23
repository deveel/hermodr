using CloudNative.CloudEvents;
using Microsoft.Extensions.DependencyInjection;

namespace Hermodr;

[Trait("Category", "Unit")]
[Trait("Feature", "DeliveryLog")]
[Trait("Function", "ErrorHandler")]
public class DeliveryLogPublishErrorHandlerTests
{
    private static CloudEvent MakeEvent(string type = "test.event")
        => new()
        {
            Type = type,
            Source = new Uri("https://example.com"),
            Id = Guid.NewGuid().ToString("N"),
        };

    [Fact]
    public async Task Should_RecordFailedDelivery_When_ChannelThrows()
    {
        var store = new InMemoryEventDeliveryLogRepository();
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton<IEventDeliveryLogRepository>(store);
        services.AddSingleton<IEventPublishDeliveryLog>(store);

        services.AddEventPublisher(opts =>
                opts.Source = new Uri("https://example.com"))
            .AddDeliveryLog(log => log.UseErrorHandler())
            .AddTestChannel(_ => throw new InvalidOperationException("Channel failure"));

        await using var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<IEventPublisher>();
        var evt = MakeEvent();

        await publisher.PublishEventAsync(evt, cancellationToken: TestContext.Current.CancellationToken);

        var records = await store.GetByEventIdAsync(evt.Id!, TestContext.Current.CancellationToken);
        var failed = records.Where(r => r.Outcome == EventDeliveryOutcome.Failed).ToList();
        Assert.NotEmpty(failed);

        var failure = failed[0];
        Assert.Equal("InvalidOperationException", failure.ErrorCode);
        Assert.Contains("Channel failure", failure.ErrorMessage);
    }

    [Fact]
    public async Task Should_SetOutcomeToFailed()
    {
        var store = new InMemoryEventDeliveryLogRepository();
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton<IEventDeliveryLogRepository>(store);
        services.AddSingleton<IEventPublishDeliveryLog>(store);

        services.AddEventPublisher(opts =>
                opts.Source = new Uri("https://example.com"))
            .AddDeliveryLog(log => log.UseErrorHandler())
            .AddTestChannel(_ => throw new Exception("fail"));

        await using var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<IEventPublisher>();
        var evt = MakeEvent();

        await publisher.PublishEventAsync(evt, cancellationToken: TestContext.Current.CancellationToken);

        var records = await store.GetByEventIdAsync(evt.Id!, TestContext.Current.CancellationToken);
        var failed = records.Where(r => r.Outcome == EventDeliveryOutcome.Failed).ToList();
        Assert.NotEmpty(failed);
        Assert.All(failed, r => Assert.Equal(EventDeliveryOutcome.Failed, r.Outcome));
    }

    [Fact]
    public async Task Should_RecordChannelName()
    {
        var store = new InMemoryEventDeliveryLogRepository();
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton<IEventDeliveryLogRepository>(store);
        services.AddSingleton<IEventPublishDeliveryLog>(store);

        services.AddEventPublisher(opts =>
                opts.Source = new Uri("https://example.com"))
            .AddDeliveryLog(log => log.UseErrorHandler())
            .AddTestChannel(_ => throw new Exception("fail"), channelName: "test-chn");

        await using var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<IEventPublisher>();
        var evt = MakeEvent();

        await publisher.PublishEventAsync(evt, cancellationToken: TestContext.Current.CancellationToken);

        var records = await store.GetByEventIdAsync(evt.Id!, TestContext.Current.CancellationToken);
        var failed = records.Where(r => r.Outcome == EventDeliveryOutcome.Failed).ToList();
        Assert.NotEmpty(failed);
        Assert.Equal("test-chn", failed[0].ChannelName);
    }

    [Fact]
    public async Task Should_NotRecord_When_WithThrowOnErrors_IsFalse()
    {
        // With ThrowOnErrors = false (default), the middleware records a Succeeded record
        // and the error handler adds failure records per-channel
        var store = new InMemoryEventDeliveryLogRepository();
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton<IEventDeliveryLogRepository>(store);
        services.AddSingleton<IEventPublishDeliveryLog>(store);

        services.AddEventPublisher(opts =>
                opts.Source = new Uri("https://example.com"))
            .AddDeliveryLog(log => log.UseErrorHandler())
            .AddTestChannel(_ => throw new Exception("fail"));

        await using var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<IEventPublisher>();
        var evt = MakeEvent();

        await publisher.PublishEventAsync(evt, cancellationToken: TestContext.Current.CancellationToken);

        // Should have both a middleware record (Succeeded) and an error handler record (Failed)
        var records = await store.GetByEventIdAsync(evt.Id!, TestContext.Current.CancellationToken);
        Assert.Equal(2, records.Count);
        Assert.Contains(records, r => r.Outcome == EventDeliveryOutcome.Succeeded);
        Assert.Contains(records, r => r.Outcome == EventDeliveryOutcome.Failed);
    }

    [Fact]
    public async Task Should_ThrowOnNullDeliveryLog_InConstructor()
    {
        Assert.Throws<ArgumentNullException>(
            () => new DeliveryLogPublishErrorHandler(null!));
    }
}
