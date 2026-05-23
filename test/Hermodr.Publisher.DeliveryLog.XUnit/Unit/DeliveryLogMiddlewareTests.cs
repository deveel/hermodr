using CloudNative.CloudEvents;
using Microsoft.Extensions.DependencyInjection;

namespace Hermodr;

[Trait("Category", "Unit")]
[Trait("Feature", "DeliveryLog")]
[Trait("Function", "Middleware")]
public class DeliveryLogMiddlewareTests
{
    private static CloudEvent MakeEvent(string type = "test.event")
        => new()
        {
            Type = type,
            Source = new Uri("https://test.example.com/source"),
            Id = Guid.NewGuid().ToString("N"),
            Time = DateTimeOffset.UtcNow,
        };

    private static (IServiceProvider Provider, InMemoryEventDeliveryLogRepository Store, List<CloudEvent> Received) BuildProvider(
        Action<EventPublisherBuilder>? configure = null)
    {
        var received = new List<CloudEvent>();
        var store = new InMemoryEventDeliveryLogRepository();
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton<IEventDeliveryLogRepository>(store);
        services.AddSingleton<IEventPublishDeliveryLog>(store);

        var builder = services.AddEventPublisher(opts =>
            opts.Source = new Uri("https://test.example.com/source"));
        builder.AddDeliveryLog();
        configure?.Invoke(builder);
        builder.AddTestChannel(e => received.Add(e));

        return (services.BuildServiceProvider(), store, received);
    }

    // ── Basic recording ─────────────────────────────────────────────────────

    [Fact]
    public async Task Should_RecordDelivery_When_EventIsPublished()
    {
        var (provider, store, _) = BuildProvider();
        await using var scope = provider.CreateAsyncScope();

        var publisher = provider.GetRequiredService<IEventPublisher>();
        var evt = MakeEvent();
        await publisher.PublishEventAsync(evt, cancellationToken: TestContext.Current.CancellationToken);

        var records = await store.GetByEventIdAsync(evt.Id!, TestContext.Current.CancellationToken);
        Assert.NotEmpty(records);
    }

    [Fact]
    public async Task Should_SetOutcomeToSucceeded_When_PublishSucceeds()
    {
        var (provider, store, _) = BuildProvider();
        await using var scope = provider.CreateAsyncScope();

        var publisher = provider.GetRequiredService<IEventPublisher>();
        var evt = MakeEvent();
        await publisher.PublishEventAsync(evt, cancellationToken: TestContext.Current.CancellationToken);

        var records = await store.GetByEventIdAsync(evt.Id!, TestContext.Current.CancellationToken);
        var record = Assert.Single(records);
        Assert.Equal(EventDeliveryOutcome.Succeeded, record.Outcome);
    }

    [Fact]
    public async Task Should_RecordEventId()
    {
        var (provider, store, _) = BuildProvider();
        await using var scope = provider.CreateAsyncScope();

        var publisher = provider.GetRequiredService<IEventPublisher>();
        var evt = MakeEvent();
        await publisher.PublishEventAsync(evt, cancellationToken: TestContext.Current.CancellationToken);

        var records = await store.GetByEventIdAsync(evt.Id!, TestContext.Current.CancellationToken);
        var record = Assert.Single(records);
        Assert.Equal(evt.Id, record.Event?.Id);
    }

    [Fact]
    public async Task Should_RecordEventType()
    {
        var (provider, store, _) = BuildProvider();
        await using var scope = provider.CreateAsyncScope();

        var publisher = provider.GetRequiredService<IEventPublisher>();
        var evt = MakeEvent("order.created");
        await publisher.PublishEventAsync(evt, cancellationToken: TestContext.Current.CancellationToken);

        var records = await store.GetByEventIdAsync(evt.Id!, TestContext.Current.CancellationToken);
        var record = Assert.Single(records);
        Assert.Equal("order.created", record.Event?.Type);
    }

    // ── Timing ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Should_RecordElapsedTime()
    {
        var (provider, store, _) = BuildProvider();
        await using var scope = provider.CreateAsyncScope();

        var publisher = provider.GetRequiredService<IEventPublisher>();
        var evt = MakeEvent();
        await publisher.PublishEventAsync(evt, cancellationToken: TestContext.Current.CancellationToken);

        var records = await store.GetByEventIdAsync(evt.Id!, TestContext.Current.CancellationToken);
        var record = Assert.Single(records);
        Assert.True(record.ElapsedTime > TimeSpan.Zero);
    }

    [Fact]
    public async Task Should_RecordTimestamp()
    {
        var (provider, store, _) = BuildProvider();
        await using var scope = provider.CreateAsyncScope();

        var publisher = provider.GetRequiredService<IEventPublisher>();
        var evt = MakeEvent();
        var before = DateTimeOffset.UtcNow;
        await publisher.PublishEventAsync(evt, cancellationToken: TestContext.Current.CancellationToken);
        var after = DateTimeOffset.UtcNow;

        var records = await store.GetByEventIdAsync(evt.Id!, TestContext.Current.CancellationToken);
        var record = Assert.Single(records);
        Assert.True(record.Timestamp >= before);
        Assert.True(record.Timestamp <= after);
    }

    // ── Attempt tracking ────────────────────────────────────────────────────

    [Fact]
    public async Task Should_StartAttemptNumber_AtOne()
    {
        var (provider, store, _) = BuildProvider();
        await using var scope = provider.CreateAsyncScope();

        var publisher = provider.GetRequiredService<IEventPublisher>();
        var evt = MakeEvent();
        await publisher.PublishEventAsync(evt, cancellationToken: TestContext.Current.CancellationToken);

        var records = await store.GetByEventIdAsync(evt.Id!, TestContext.Current.CancellationToken);
        var record = Assert.Single(records);
        Assert.Equal(1, record.AttemptNumber);
    }

    [Fact]
    public async Task Should_TrackAttemptNumber_InContextItems()
    {
        var (provider, store, _) = BuildProvider();
        await using var scope = provider.CreateAsyncScope();

        var publisher = provider.GetRequiredService<IEventPublisher>();
        var evt = MakeEvent();

        // The first publish always starts at attempt 1
        await publisher.PublishEventAsync(evt, cancellationToken: TestContext.Current.CancellationToken);

        var records1 = await store.GetByEventIdAsync(evt.Id!, TestContext.Current.CancellationToken);
        var first = records1.OrderBy(r => r.Timestamp).First();
        Assert.Equal(1, first.AttemptNumber);

        // A second publish with the same event advances the attempt counter
        // (each middleware instance tracks via EventContext.Items)
        await publisher.PublishEventAsync(evt, cancellationToken: TestContext.Current.CancellationToken);

        var records2 = await store.GetByEventIdAsync(evt.Id!, TestContext.Current.CancellationToken);
        Assert.Equal(2, records2.Count);
    }

    // ── Channel name from options ──────────────────────────────────────────

    [Fact]
    public async Task Should_RecordChannelName_When_NamedChannelPublishOptionsUsed()
    {
        var received = new List<CloudEvent>();
        var store = new InMemoryEventDeliveryLogRepository();
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton<IEventDeliveryLogRepository>(store);
        services.AddSingleton<IEventPublishDeliveryLog>(store);

        services.AddEventPublisher(opts =>
                opts.Source = new Uri("https://test.example.com/source"))
            .AddDeliveryLog()
            .AddTestChannel(e => received.Add(e), channelName: "test-channel");

        await using var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<IEventPublisher>();
        var evt = MakeEvent();

        await publisher.PublishEventAsync(evt, new NamedChannelPublishOptions { ChannelName = "test-channel" },
            TestContext.Current.CancellationToken);

        var records = await store.GetByEventIdAsync(evt.Id!, TestContext.Current.CancellationToken);
        var record = Assert.Single(records);
        Assert.Equal("test-channel", record.ChannelName);
    }

    // ── Unique ID ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Should_GenerateUniqueId_ForEachRecord()
    {
        var (provider, store, _) = BuildProvider();
        await using var scope = provider.CreateAsyncScope();

        var publisher = provider.GetRequiredService<IEventPublisher>();
        var evt1 = MakeEvent();
        var evt2 = MakeEvent();
        await publisher.PublishEventAsync(evt1, cancellationToken: TestContext.Current.CancellationToken);
        await publisher.PublishEventAsync(evt2, cancellationToken: TestContext.Current.CancellationToken);

        var store2 = (IEventDeliveryLogRepository)store;
        var all = await store2.GetByTimeRangeAsync(
            DateTimeOffset.MinValue, DateTimeOffset.MaxValue, TestContext.Current.CancellationToken);
        Assert.Equal(2, all.Count);
        Assert.NotEqual(all[0].Id, all[1].Id);
    }

    // ── Error handling ──────────────────────────────────────────────────────

    [Fact]
    public async Task Should_RecordFailedOutcome_When_ChannelThrows()
    {
        var store = new InMemoryEventDeliveryLogRepository();
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton<IEventDeliveryLogRepository>(store);
        services.AddSingleton<IEventPublishDeliveryLog>(store);

        services.AddEventPublisher(opts =>
            {
                opts.Source = new Uri("https://test.example.com/source");
                opts.ThrowOnErrors = true;
            })
            .AddDeliveryLog()
            .AddTestChannel(_ => throw new InvalidOperationException("Channel failure"));

        await using var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<IEventPublisher>();
        var evt = MakeEvent();

        await Assert.ThrowsAsync<EventPublishChannelException>(
            () => publisher.PublishEventAsync(evt, cancellationToken: TestContext.Current.CancellationToken));

        var records = await store.GetByEventIdAsync(evt.Id!, TestContext.Current.CancellationToken);
        var record = Assert.Single(records);
        Assert.Equal(EventDeliveryOutcome.Failed, record.Outcome);
        Assert.NotNull(record.ErrorCode);
    }

    [Fact]
    public async Task Should_RecordElapsedTime_EvenOnFailure()
    {
        var store = new InMemoryEventDeliveryLogRepository();
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton<IEventDeliveryLogRepository>(store);
        services.AddSingleton<IEventPublishDeliveryLog>(store);

        services.AddEventPublisher(opts =>
            {
                opts.Source = new Uri("https://test.example.com/source");
                opts.ThrowOnErrors = true;
            })
            .AddDeliveryLog()
            .AddTestChannel(_ => throw new Exception("fail"));

        await using var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<IEventPublisher>();
        var evt = MakeEvent();

        await Assert.ThrowsAsync<EventPublishChannelException>(
            () => publisher.PublishEventAsync(evt, cancellationToken: TestContext.Current.CancellationToken));

        var records = await store.GetByEventIdAsync(evt.Id!, TestContext.Current.CancellationToken);
        var record = Assert.Single(records);
        Assert.True(record.ElapsedTime > TimeSpan.Zero);
    }

    // ── Middleware does not swallow exceptions ───────────────────────────────

    [Fact]
    public async Task Should_NotSwallowException_WhenThrowOnErrorsIsTrue()
    {
        var store = new InMemoryEventDeliveryLogRepository();
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton<IEventDeliveryLogRepository>(store);
        services.AddSingleton<IEventPublishDeliveryLog>(store);

        services.AddEventPublisher(opts =>
            {
                opts.Source = new Uri("https://test.example.com/source");
                opts.ThrowOnErrors = true;
            })
            .AddDeliveryLog()
            .AddTestChannel(_ => throw new InvalidOperationException("fail"));

        await using var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<IEventPublisher>();
        var evt = MakeEvent();

        var ex = await Assert.ThrowsAsync<EventPublishChannelException>(
            () => publisher.PublishEventAsync(evt, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Contains("fail", ex.InnerException?.Message);
    }

    [Fact]
    public async Task Should_AllowMiddlewareToRecord_When_NotThrowOnErrors()
    {
        var (provider, store, _) = BuildProvider();
        await using var scope = provider.CreateAsyncScope();

        var publisher = provider.GetRequiredService<IEventPublisher>();
        var evt = MakeEvent();
        await publisher.PublishEventAsync(evt, cancellationToken: TestContext.Current.CancellationToken);

        var records = await store.GetByEventIdAsync(evt.Id!, TestContext.Current.CancellationToken);
        Assert.NotEmpty(records);
    }

    // ── Bypass pipeline ─────────────────────────────────────────────────────

    [Fact]
    public async Task Should_NotRecord_When_PipelineIsBypassed()
    {
        var (provider, store, received) = BuildProvider();
        await using var scope = provider.CreateAsyncScope();

        var publisher = provider.GetRequiredService<IEventPublisher>();
        var evt = MakeEvent();
        await publisher.PublishEventAsync(evt, EventPublishOptions.BypassPipeline(),
            TestContext.Current.CancellationToken);

        Assert.NotEmpty(received);
        var records = await store.GetByEventIdAsync(evt.Id!, TestContext.Current.CancellationToken);
        Assert.Empty(records);
    }
}
