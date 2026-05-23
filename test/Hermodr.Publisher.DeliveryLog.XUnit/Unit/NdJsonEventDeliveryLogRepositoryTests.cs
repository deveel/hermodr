using Bogus;
using CloudNative.CloudEvents;
using Microsoft.Extensions.Options;

namespace Hermodr;

[Trait("Category", "Unit")]
[Trait("Layer", "Infrastructure")]
[Trait("Feature", "DeliveryLog")]
public class NdJsonEventDeliveryLogRepositoryTests : IDisposable
{
    private static readonly Faker Faker = new("en");
    private readonly string _tempDir;

    public NdJsonEventDeliveryLogRepositoryTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ndjson-test-{Guid.NewGuid():N}");
    }

    private NdJsonDeliveryLogOptions CreateOptions(int maxFileCount = 10)
    {
        return new NdJsonDeliveryLogOptions
        {
            DirectoryPath = _tempDir,
            MaxFileSizeBytes = 10 * 1024 * 1024,
            MaxFileCount = maxFileCount
        };
    }

    private EventDeliveryRecord CreateRecord()
    {
        return new EventDeliveryRecord
        {
            Id = Faker.Random.Guid().ToString("N"),
            Event = new CloudEvent
            {
                Id = Faker.Random.Guid().ToString("N"),
                Type = "test.event",
                Source = new Uri("urn:test")
            },
            ChannelName = Faker.Internet.DomainWord(),
            AttemptNumber = Faker.Random.Int(1, 5),
            Timestamp = DateTimeOffset.UtcNow.AddMinutes(-Faker.Random.Int(0, 60)),
            Outcome = EventDeliveryOutcome.Succeeded,
            ElapsedTime = TimeSpan.FromMilliseconds(Faker.Random.Int(10, 500))
        };
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, true); }
            catch (IOException) { /* cleanup best-effort */ }
            catch (UnauthorizedAccessException) { /* cleanup best-effort */ }
        }
    }

    // ── Constructor ─────────────────────────────────────────────────────────

    [Fact]
    public void Should_CreateDirectory_When_NotExists()
    {
        Assert.False(Directory.Exists(_tempDir));

        using var store = new NdJsonEventDeliveryLogRepository(
            Options.Create(CreateOptions()));

        Assert.True(Directory.Exists(_tempDir));
    }

    [Fact]
    public void Should_ThrowOnNullOptions()
    {
        Assert.Throws<ArgumentNullException>(
            () => new NdJsonEventDeliveryLogRepository(null!));
    }

    // ── RecordAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Should_RecordDelivery_ToFile()
    {
        using var store = new NdJsonEventDeliveryLogRepository(
            Options.Create(CreateOptions()));
        var record = CreateRecord();

        await store.RecordAsync(record, TestContext.Current.CancellationToken);

        var files = Directory.GetFiles(_tempDir, "*.ndjson");
        Assert.NotEmpty(files);
    }

    [Fact]
    public async Task Should_ThrowArgumentNullException_When_RecordIsNull()
    {
        using var store = new NdJsonEventDeliveryLogRepository(
            Options.Create(CreateOptions()));

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => store.RecordAsync(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_WriteMultipleRecords()
    {
        using var store = new NdJsonEventDeliveryLogRepository(
            Options.Create(CreateOptions()));

        for (var i = 0; i < 10; i++)
        {
            await store.RecordAsync(CreateRecord(), TestContext.Current.CancellationToken);
        }

        var files = Directory.GetFiles(_tempDir, "*.ndjson");
        Assert.Single(files);

        var lines = await File.ReadAllLinesAsync(files[0], TestContext.Current.CancellationToken);
        Assert.Equal(10, lines.Length);
    }

    // ── Query ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Should_QueryByEventId()
    {
        using var store = new NdJsonEventDeliveryLogRepository(
            Options.Create(CreateOptions()));
        var record = CreateRecord();

        await store.RecordAsync(record, TestContext.Current.CancellationToken);

        var results = await store.GetByEventIdAsync(
            record.Event!.Id, TestContext.Current.CancellationToken);

        Assert.Single(results);
        Assert.Equal(record.Id, results[0].Id);
    }

    [Fact]
    public async Task Should_QueryByChannel()
    {
        using var store = new NdJsonEventDeliveryLogRepository(
            Options.Create(CreateOptions()));
        var record = CreateRecord();

        await store.RecordAsync(record, TestContext.Current.CancellationToken);

        var results = await store.GetByChannelAsync(
            record.ChannelName!, TestContext.Current.CancellationToken);

        Assert.Single(results);
        Assert.Equal(record.Id, results[0].Id);
    }

    [Fact]
    public async Task Should_QueryByOutcome()
    {
        using var store = new NdJsonEventDeliveryLogRepository(
            Options.Create(CreateOptions()));

        await store.RecordAsync(CreateRecord(), TestContext.Current.CancellationToken);
        var failedRecord = CreateRecord();
        failedRecord.Outcome = EventDeliveryOutcome.Failed;
        await store.RecordAsync(failedRecord, TestContext.Current.CancellationToken);

        var results = await store.GetByOutcomeAsync(
            EventDeliveryOutcome.Failed, TestContext.Current.CancellationToken);

        Assert.Single(results);
        Assert.Equal(EventDeliveryOutcome.Failed, results[0].Outcome);
    }

    [Fact]
    public async Task Should_QueryByTimeRange()
    {
        using var store = new NdJsonEventDeliveryLogRepository(
            Options.Create(CreateOptions()));
        var record = CreateRecord();

        await store.RecordAsync(record, TestContext.Current.CancellationToken);

        var from = record.Timestamp.AddMinutes(-1);
        var to = record.Timestamp.AddMinutes(1);
        var results = await store.GetByTimeRangeAsync(
            from, to, TestContext.Current.CancellationToken);

        Assert.Single(results);
    }

    // ── Storage backend ─────────────────────────────────────────────────────

    [Fact]
    public void Should_Implement_IStorageBackend()
    {
        using var store = new NdJsonEventDeliveryLogRepository(
            Options.Create(CreateOptions()));

        Assert.Equal("NDJson", store.ProviderName);
    }

    // ── Dispose ─────────────────────────────────────────────────────────────

    [Fact]
    public void Should_Implement_IDisposable()
    {
        using var store = new NdJsonEventDeliveryLogRepository(
            Options.Create(CreateOptions()));

        // Should not throw
        store.Dispose();
    }
}
