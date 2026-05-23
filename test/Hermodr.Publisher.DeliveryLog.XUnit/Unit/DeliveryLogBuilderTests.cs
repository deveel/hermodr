using CloudNative.CloudEvents;
using Microsoft.Extensions.DependencyInjection;

namespace Hermodr;

[Trait("Category", "Unit")]
[Trait("Feature", "DeliveryLog")]
[Trait("Function", "Registration")]
public class DeliveryLogBuilderTests
{
    private static CloudEvent MakeEvent(string type = "test.event")
        => new()
        {
            Type = type,
            Source = new Uri("https://example.com"),
            Id = Guid.NewGuid().ToString("N"),
        };

    // ── AddDeliveryLog on EventPublisherBuilder ──────────────────────────────

    [Fact]
    public async Task Should_RegisterInMemoryStore_ByDefault()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddEventPublisher(opts =>
                opts.Source = new Uri("https://example.com"))
            .AddDeliveryLog()
            .AddTestChannel(_ => { });

        await using var provider = services.BuildServiceProvider();
        var store = provider.GetService<IEventDeliveryLogRepository>();

        Assert.NotNull(store);
        Assert.IsType<InMemoryEventDeliveryLogRepository>(store);
    }

    [Fact]
    public async Task Should_RegisterDeliveryLog_AsSingleton()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddEventPublisher(opts =>
                opts.Source = new Uri("https://example.com"))
            .AddDeliveryLog()
            .AddTestChannel(_ => { });

        await using var provider = services.BuildServiceProvider();
        var store1 = provider.GetRequiredService<IEventDeliveryLogRepository>();
        var store2 = provider.GetRequiredService<IEventDeliveryLogRepository>();

        Assert.Same(store1, store2);
    }

    [Fact]
    public async Task Should_RegisterMiddleware_ThatRecordsDeliveries()
    {
        var store = new InMemoryEventDeliveryLogRepository();
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton<IEventDeliveryLogRepository>(store);
        services.AddSingleton<IEventPublishDeliveryLog>(store);

        services.AddEventPublisher(opts =>
                opts.Source = new Uri("https://example.com"))
            .AddDeliveryLog()
            .AddTestChannel(_ => { });

        await using var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<IEventPublisher>();
        var evt = MakeEvent();

        await publisher.PublishEventAsync(evt, cancellationToken: TestContext.Current.CancellationToken);

        var records = await store.GetByEventIdAsync(evt.Id!, TestContext.Current.CancellationToken);
        Assert.NotEmpty(records);
    }

    // ── UseInMemory ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Should_ResolveAsWriteInterface()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddEventPublisher(opts =>
                opts.Source = new Uri("https://example.com"))
            .AddDeliveryLog()
            .AddTestChannel(_ => { });

        await using var provider = services.BuildServiceProvider();
        var writeLog = provider.GetRequiredService<IEventPublishDeliveryLog>();

        Assert.NotNull(writeLog);
    }

    [Fact]
    public async Task Should_ResolveAsQueryInterface()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddEventPublisher(opts =>
                opts.Source = new Uri("https://example.com"))
            .AddDeliveryLog()
            .AddTestChannel(_ => { });

        await using var provider = services.BuildServiceProvider();
        var queryStore = provider.GetRequiredService<IEventDeliveryLogRepository>();

        Assert.NotNull(queryStore);
    }

    // ── UseErrorHandler ─────────────────────────────────────────────────────

    [Fact]
    public async Task Should_RegisterErrorHandler_When_UseErrorHandlerCalled()
    {
        var store = new InMemoryEventDeliveryLogRepository();
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton<IEventDeliveryLogRepository>(store);
        services.AddSingleton<IEventPublishDeliveryLog>(store);

        services.AddEventPublisher(opts =>
                opts.Source = new Uri("https://example.com"))
            .AddDeliveryLog(log => log.UseErrorHandler())
            .AddTestChannel(_ => throw new InvalidOperationException("fail"));

        await using var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<IEventPublisher>();
        var evt = MakeEvent();

        await publisher.PublishEventAsync(evt, cancellationToken: TestContext.Current.CancellationToken);

        var records = await store.GetByEventIdAsync(evt.Id!, TestContext.Current.CancellationToken);
        var failed = records.Where(r => r.Outcome == EventDeliveryOutcome.Failed).ToList();
        Assert.NotEmpty(failed);
    }

    // ── UseStore ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Should_AcceptCustomStoreInstance()
    {
        var customStore = new InMemoryEventDeliveryLogRepository();
        var services = new ServiceCollection().AddLogging();

        services.AddEventPublisher(opts =>
                opts.Source = new Uri("https://example.com"))
            .AddDeliveryLog(log => log.UseStore(customStore))
            .AddTestChannel(_ => { });

        await using var provider = services.BuildServiceProvider();
        var resolved = provider.GetRequiredService<IEventDeliveryLogRepository>();

        Assert.Same(customStore, resolved);
    }

    [Fact]
    public async Task Should_ThrowArgumentNullException_When_StoreIsNull()
    {
        var services = new ServiceCollection().AddLogging();

        var ex = Assert.Throws<ArgumentNullException>(() =>
        {
            services.AddEventPublisher(opts =>
                    opts.Source = new Uri("https://example.com"))
                .AddDeliveryLog(log => log.UseStore((IEventDeliveryLogRepository)null!));
        });

        Assert.Contains("store", ex.Message);
    }

    // ── Standalone AddDeliveryLog ────────────────────────────────────────────

    [Fact]
    public async Task Should_RegisterStandaloneDeliveryLog()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddDeliveryLog();

        await using var provider = services.BuildServiceProvider();
        var writeLog = provider.GetRequiredService<IEventPublishDeliveryLog>();
        var queryStore = provider.GetRequiredService<IEventDeliveryLogRepository>();

        Assert.NotNull(writeLog);
        Assert.NotNull(queryStore);
        Assert.IsType<InMemoryEventDeliveryLogRepository>(writeLog);
        Assert.Same(writeLog, queryStore);
    }

    [Fact]
    public async Task Should_RecordThroughStandaloneDeliveryLog()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddDeliveryLog();

        await using var provider = services.BuildServiceProvider();
        var log = provider.GetRequiredService<IEventPublishDeliveryLog>();
        var record = new EventDeliveryRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            Event = new CloudEvent
            {
                Id = Guid.NewGuid().ToString("N"),
                Type = "test.event",
                Source = new Uri("urn:test")
            },
            AttemptNumber = 1,
            Timestamp = DateTimeOffset.UtcNow,
            Outcome = EventDeliveryOutcome.Succeeded,
            ElapsedTime = TimeSpan.FromMilliseconds(100)
        };

        await log.RecordAsync(record, TestContext.Current.CancellationToken);

        var store = provider.GetRequiredService<IEventDeliveryLogRepository>();
        var results = await store.GetByEventIdAsync(record.Event!.Id, TestContext.Current.CancellationToken);
        Assert.Single(results);
    }
}
