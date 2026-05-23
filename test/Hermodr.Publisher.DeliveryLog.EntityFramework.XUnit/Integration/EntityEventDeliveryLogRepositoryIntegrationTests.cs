using Bogus;
using CloudNative.CloudEvents;

using Deveel.Data;

using Microsoft.Extensions.DependencyInjection;

namespace Hermodr;

[Trait("Category", "Integration")]
[Trait("Feature", "DeliveryLog")]
[Trait("Layer", "Infrastructure")]
public class EntityEventDeliveryLogRepositoryIntegrationTests : IClassFixture<SqliteDeliveryLogFixture>, IAsyncLifetime
{
    private readonly SqliteDeliveryLogFixture _db;
    private static readonly Faker Faker = new("en");

    public EntityEventDeliveryLogRepositoryIntegrationTests(SqliteDeliveryLogFixture db)
    {
        _db = db;
    }

    public async ValueTask InitializeAsync()
    {
        using var ctx = _db.CreateContext();
        ctx.DeliveryRecords.RemoveRange(ctx.DeliveryRecords);
        await ctx.SaveChangesAsync();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static EventDeliveryRecord CreateRecord(string? channelName = null, EventDeliveryOutcome? outcome = null)
    {
        return new EventDeliveryRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            Event = new CloudEvent
            {
                Id = Guid.NewGuid().ToString("N"),
                Type = "test.event",
                Source = new Uri("urn:test")
            },
            PublisherName = "default",
            ChannelName = channelName ?? Faker.Internet.DomainWord(),
            ChannelType = "memory",
            AttemptNumber = Faker.Random.Int(1, 5),
            Timestamp = DateTimeOffset.UtcNow.AddMinutes(-Faker.Random.Int(0, 120)),
            Outcome = outcome ?? EventDeliveryOutcome.Succeeded,
            ErrorCode = outcome == EventDeliveryOutcome.Failed ? "ERR_001" : null,
            ErrorMessage = outcome == EventDeliveryOutcome.Failed ? "Something went wrong" : null,
            ElapsedTime = TimeSpan.FromMilliseconds(Faker.Random.Int(10, 5000))
        };
    }

    [Fact]
    public async Task Should_ResolveServices_ThroughPipeline()
    {
        var writeLog = _db.GetService<IEventPublishDeliveryLog>();
        var queryStore = _db.GetService<IEventDeliveryLogRepository>();
        var context = _db.GetService<DeliveryLogDbContext>();

        Assert.NotNull(writeLog);
        Assert.NotNull(queryStore);
        Assert.NotNull(context);
        Assert.IsType<EntityEventDeliveryLogRepository>(writeLog);
    }

    [Fact]
    public async Task Should_StoreAndRetrieve_ByEventId()
    {
        var repo = _db.CreateRepository();

        var record = CreateRecord();
        await repo.RecordAsync(record, TestContext.Current.CancellationToken);

        var results = await repo.GetByEventIdAsync(record.Event!.Id, TestContext.Current.CancellationToken);
        var found = Assert.Single(results);
        Assert.Equal(record.Id, found.Id);
        Assert.Equal(record.Event.Type, found.Event!.Type);
        Assert.Equal(record.Outcome, found.Outcome);
        Assert.Equal(record.ChannelName, found.ChannelName);
        Assert.Equal(record.AttemptNumber, found.AttemptNumber);
        Assert.Equal(record.Timestamp, found.Timestamp);
        Assert.Equal(record.ElapsedTime, found.ElapsedTime);
    }

    [Fact]
    public async Task Should_StoreAndRetrieve_ByChannel()
    {
        var repo = _db.CreateRepository();

        var channel = "orders-service";
        var record = CreateRecord(channelName: channel);
        await repo.RecordAsync(record, TestContext.Current.CancellationToken);

        var results = await repo.GetByChannelAsync(channel, TestContext.Current.CancellationToken);
        var found = Assert.Single(results);
        Assert.Equal(record.Id, found.Id);
    }

    [Fact]
    public async Task Should_StoreAndRetrieve_ByOutcome()
    {
        var repo = _db.CreateRepository();

        var record = CreateRecord(outcome: EventDeliveryOutcome.Failed);
        await repo.RecordAsync(record, TestContext.Current.CancellationToken);

        var results = await repo.GetByOutcomeAsync(EventDeliveryOutcome.Failed, TestContext.Current.CancellationToken);
        var found = Assert.Single(results);
        Assert.Equal(record.Id, found.Id);
        Assert.Equal(EventDeliveryOutcome.Failed, found.Outcome);
    }

    [Fact]
    public async Task Should_StoreAndRetrieve_ByTimeRange()
    {
        var repo = _db.CreateRepository();

        var now = DateTimeOffset.UtcNow;
        var early = CreateRecord();
        early.Timestamp = now.AddHours(-5);
        var late = CreateRecord();
        late.Timestamp = now.AddHours(-1);

        await repo.RecordAsync(early, TestContext.Current.CancellationToken);
        await repo.RecordAsync(late, TestContext.Current.CancellationToken);

        var results = await repo.GetByTimeRangeAsync(
            now.AddHours(-6), now.AddHours(-2), TestContext.Current.CancellationToken);
        var found = Assert.Single(results);
        Assert.Equal(early.Id, found.Id);
    }

    [Fact]
    public async Task Should_ReturnRecords_OrderedByTimestamp()
    {
        var repo = _db.CreateRepository();
        var eventId = Guid.NewGuid().ToString("N");

        for (int i = 0; i < 5; i++)
        {
            var record = CreateRecord();
            record.Event!.Id = eventId;
            record.Timestamp = DateTimeOffset.UtcNow.AddHours(-i);
            await repo.RecordAsync(record, TestContext.Current.CancellationToken);
        }

        var results = await repo.GetByEventIdAsync(eventId, TestContext.Current.CancellationToken);

        Assert.Equal(5, results.Count);
        for (int i = 1; i < results.Count; i++)
            Assert.True(results[i - 1].Timestamp <= results[i].Timestamp);
    }

    [Fact]
    public async Task Should_ReturnRecords_OrderedByTimestampDescending_ForChannel()
    {
        var repo = _db.CreateRepository();
        var channel = "notifications";

        for (int i = 0; i < 5; i++)
        {
            var record = CreateRecord(channelName: channel);
            record.Timestamp = DateTimeOffset.UtcNow.AddHours(-i);
            await repo.RecordAsync(record, TestContext.Current.CancellationToken);
        }

        var results = await repo.GetByChannelAsync(channel, TestContext.Current.CancellationToken);

        Assert.Equal(5, results.Count);
        for (int i = 1; i < results.Count; i++)
            Assert.True(results[i - 1].Timestamp >= results[i].Timestamp);
    }

    [Fact]
    public async Task Should_CombineMultipleFilters()
    {
        var repo = _db.CreateRepository();
        var eventId = Guid.NewGuid().ToString("N");

        var matched = CreateRecord(channelName: "billing", outcome: EventDeliveryOutcome.Failed);
        matched.Event!.Id = eventId;
        matched.Timestamp = DateTimeOffset.UtcNow.AddHours(-2);

        await repo.RecordAsync(matched, TestContext.Current.CancellationToken);
        await repo.RecordAsync(CreateRecord(channelName: "billing", outcome: EventDeliveryOutcome.Succeeded), TestContext.Current.CancellationToken);
        await repo.RecordAsync(CreateRecord(channelName: "orders", outcome: EventDeliveryOutcome.Failed), TestContext.Current.CancellationToken);

        var byEvent = await repo.GetByEventIdAsync(eventId, TestContext.Current.CancellationToken);
        var byChannel = await repo.GetByChannelAsync("billing", TestContext.Current.CancellationToken);
        var byOutcome = await repo.GetByOutcomeAsync(EventDeliveryOutcome.Failed, TestContext.Current.CancellationToken);

        Assert.Single(byEvent);
        Assert.Equal(2, byChannel.Count);
        Assert.Equal(2, byOutcome.Count);
    }

    [Fact]
    public async Task Should_RecordAndRetrieve_ThroughRepositoryInterface()
    {
        var repo = _db.CreateRepository();

        var record = CreateRecord();
        var repoInterface = (IRepository<EventDeliveryRecord, object>)repo;
        await repoInterface.AddAsync(record, TestContext.Current.CancellationToken);

        var found = await repoInterface.FindAsync(record.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(found);
        Assert.Equal(record.Id, found.Id);
        Assert.Equal(record.Event!.Id, found.Event!.Id);
    }

    [Fact]
    public async Task Should_UpdateRecord_ThroughRepositoryInterface()
    {
        var repo = _db.CreateRepository();
        var repoInterface = (IRepository<EventDeliveryRecord, object>)repo;

        var record = CreateRecord();
        await repoInterface.AddAsync(record, TestContext.Current.CancellationToken);

        var updatedRecord = await repoInterface.FindAsync(record.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(updatedRecord);

        var ctx = _db.CreateContext();
        var dbEntity = await ctx.DeliveryRecords.FindAsync([record.Id], TestContext.Current.CancellationToken);
        Assert.NotNull(dbEntity);
        Assert.Equal(record.Event!.Id, dbEntity.EventId);
    }

    [Fact]
    public async Task Should_RemoveRecord_ThroughRepositoryInterface()
    {
        var repo = _db.CreateRepository();
        var repoInterface = (IRepository<EventDeliveryRecord, object>)repo;

        var record = CreateRecord();
        await repoInterface.AddAsync(record, TestContext.Current.CancellationToken);

        var removed = await repoInterface.RemoveAsync(record, TestContext.Current.CancellationToken);
        Assert.True(removed);

        var found = await repoInterface.FindAsync(record.Id, TestContext.Current.CancellationToken);
        Assert.Null(found);
    }

    [Fact]
    public async Task Should_ReturnEmpty_WhenNoMatchingRecords()
    {
        var repo = _db.CreateRepository();

        Assert.Empty(await repo.GetByEventIdAsync("nonexistent", TestContext.Current.CancellationToken));
        Assert.Empty(await repo.GetByChannelAsync("nonexistent", TestContext.Current.CancellationToken));
        Assert.Empty(await repo.GetByOutcomeAsync(EventDeliveryOutcome.Retried, TestContext.Current.CancellationToken));
        Assert.Empty(await repo.GetByTimeRangeAsync(
            DateTimeOffset.UtcNow.AddDays(-10), DateTimeOffset.UtcNow.AddDays(-9), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_Roundtrip_AllOutcomes()
    {
        var repo = _db.CreateRepository();

        foreach (var outcome in new[] { EventDeliveryOutcome.Succeeded, EventDeliveryOutcome.Failed, EventDeliveryOutcome.Retried })
        {
            var record = CreateRecord(outcome: outcome);
            await repo.RecordAsync(record, TestContext.Current.CancellationToken);

            var results = await repo.GetByOutcomeAsync(outcome, TestContext.Current.CancellationToken);
            Assert.Contains(results, r => r.Id == record.Id);
        }
    }

    [Fact]
    public async Task Should_WriteMultipleRecords_Sequentially()
    {
        var repo = _db.CreateRepository();
        var eventId = Guid.NewGuid().ToString("N");

        for (int i = 0; i < 10; i++)
        {
            var record = CreateRecord();
            record.Event!.Id = eventId;
            await repo.RecordAsync(record, TestContext.Current.CancellationToken);
        }

        var results = await repo.GetByEventIdAsync(eventId, TestContext.Current.CancellationToken);
        Assert.Equal(10, results.Count);
    }

    [Fact]
    public async Task Should_StoreRecord_WithAllFieldsPopulated()
    {
        var repo = _db.CreateRepository();

        var record = new EventDeliveryRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            Event = new CloudEvent
            {
                Id = Guid.NewGuid().ToString("N"),
                Type = "com.example.order.created",
                Source = new Uri("urn:test")
            },
            PublisherName = "order-service",
            ChannelName = "kafka.orders",
            ChannelType = "Apache.Kafka",
            AttemptNumber = 3,
            Timestamp = new DateTimeOffset(2026, 5, 15, 14, 30, 0, TimeSpan.Zero),
            Outcome = EventDeliveryOutcome.Failed,
            ErrorCode = "TIMEOUT",
            ErrorMessage = "The request timed out after 30 seconds",
            ElapsedTime = TimeSpan.FromSeconds(30.5)
        };

        await repo.RecordAsync(record, TestContext.Current.CancellationToken);

        var results = await repo.GetByEventIdAsync(record.Event!.Id, TestContext.Current.CancellationToken);
        var found = Assert.Single(results);

        Assert.Equal(record.Id, found.Id);
        Assert.Equal(record.Event.Id, found.Event!.Id);
        Assert.Equal(record.Event.Type, found.Event.Type);
        Assert.Equal(record.PublisherName, found.PublisherName);
        Assert.Equal(record.ChannelName, found.ChannelName);
        Assert.Equal(record.ChannelType, found.ChannelType);
        Assert.Equal(record.AttemptNumber, found.AttemptNumber);
        Assert.Equal(record.Timestamp, found.Timestamp);
        Assert.Equal(record.Outcome, found.Outcome);
        Assert.Equal(record.ErrorCode, found.ErrorCode);
        Assert.Equal(record.ErrorMessage, found.ErrorMessage);
        Assert.Equal(record.ElapsedTime, found.ElapsedTime);
    }

    [Fact]
    public async Task Should_RecordAsync_BeUsable_ThroughPublishLogInterface()
    {
        var repo = _db.CreateRepository();

        var record = CreateRecord();
        var publishLog = (IEventPublishDeliveryLog)repo;
        await publishLog.RecordAsync(record, TestContext.Current.CancellationToken);

        var results = await repo.GetByEventIdAsync(record.Event!.Id, TestContext.Current.CancellationToken);
        Assert.Single(results);
    }

    [Fact]
    public async Task Should_Throw_WhenAddingNullRecord()
    {
        var repo = _db.CreateRepository();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => repo.RecordAsync(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_StoreRecords_WithNullOptionalFields()
    {
        var repo = _db.CreateRepository();

        var record = new EventDeliveryRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            Event = new CloudEvent
            {
                Id = Guid.NewGuid().ToString("N"),
                Type = "test.event",
                Source = new Uri("urn:test")
            },
            Outcome = EventDeliveryOutcome.Succeeded,
            Timestamp = DateTimeOffset.UtcNow,
            ElapsedTime = TimeSpan.Zero
        };

        await repo.RecordAsync(record, TestContext.Current.CancellationToken);

        var results = await repo.GetByEventIdAsync(record.Event!.Id, TestContext.Current.CancellationToken);
        var found = Assert.Single(results);

        Assert.Null(found.PublisherName);
        Assert.Null(found.ChannelName);
        Assert.Null(found.ChannelType);
        Assert.Null(found.ErrorCode);
        Assert.Null(found.ErrorMessage);
    }
}
