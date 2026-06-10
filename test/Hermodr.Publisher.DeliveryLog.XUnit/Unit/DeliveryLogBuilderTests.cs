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
    public async Task Should_RegisterMiddleware_ThatRecordsDeliveries()
    {
        var store = new InMemoryDeliveryStore();
        var services = new ServiceCollection().AddLogging();
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

    // ── UseStore ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Should_ResolveAsWriteInterface()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddEventPublisher(opts =>
                opts.Source = new Uri("https://example.com"))
            .AddDeliveryLog(log => log.UseStore<InMemoryDeliveryStore>())
            .AddTestChannel(_ => { });

        await using var provider = services.BuildServiceProvider();
        var writeLog = provider.GetRequiredService<IEventPublishDeliveryLog>();

        Assert.NotNull(writeLog);
    }

    // ── UseErrorHandler ─────────────────────────────────────────────────────

    [Fact]
    public async Task Should_RegisterErrorHandler_When_UseErrorHandlerCalled()
    {
        var store = new InMemoryDeliveryStore();
        var services = new ServiceCollection().AddLogging();
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
        var customStore = new InMemoryDeliveryStore();
        var services = new ServiceCollection().AddLogging();

        services.AddEventPublisher(opts =>
                opts.Source = new Uri("https://example.com"))
            .AddDeliveryLog(log => log.UseStore(customStore))
            .AddTestChannel(_ => { });

        await using var provider = services.BuildServiceProvider();
        var resolved = provider.GetRequiredService<IEventPublishDeliveryLog>();

        Assert.Same(customStore, resolved);
    }

    [Fact]
    public void Should_ThrowArgumentNullException_When_StoreIsNull()
    {
        var services = new ServiceCollection().AddLogging();

        var ex = Assert.Throws<ArgumentNullException>(() =>
        {
            services.AddEventPublisher(opts =>
                    opts.Source = new Uri("https://example.com"))
                .AddDeliveryLog(log => log.UseStore((IEventPublishDeliveryLog)null!));
        });

        Assert.Contains("store", ex.Message);
    }

    // ── Standalone AddDeliveryLog ────────────────────────────────────────────

    [Fact]
    public void Should_NotRegisterDefaultStore()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddDeliveryLog();

        using var provider = services.BuildServiceProvider();
        var store = provider.GetService<IEventPublishDeliveryLog>();

        Assert.Null(store);
    }

    [Fact]
    public async Task Should_RecordThroughConfiguredStore()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddDeliveryLog(log => log.UseStore<InMemoryDeliveryStore>());

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

        Assert.NotNull(record.Id);
    }
}
