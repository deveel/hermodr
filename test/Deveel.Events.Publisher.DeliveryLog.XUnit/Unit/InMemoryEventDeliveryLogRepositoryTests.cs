using Bogus;
using CloudNative.CloudEvents;

namespace Deveel.Events;

[Trait("Category", "Unit")]
[Trait("Layer", "Infrastructure")]
[Trait("Feature", "DeliveryLog")]
public class InMemoryEventDeliveryLogRepositoryTests
{
    private static readonly Faker Faker = new("en");

    private static EventDeliveryRecord CreateRecord(string? channelName = null, EventDeliveryOutcome? outcome = null)
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
            ChannelName = channelName ?? Faker.Internet.DomainWord(),
            AttemptNumber = 1,
            Timestamp = DateTimeOffset.UtcNow.AddMinutes(-Faker.Random.Int(0, 60)),
            Outcome = outcome ?? EventDeliveryOutcome.Succeeded,
            ElapsedTime = TimeSpan.FromMilliseconds(Faker.Random.Int(10, 500))
        };
    }

    // ── RecordAsync ─────────────────────────────────────────────────────────

    public class RecordAsyncMethod
    {
        [Fact]
        public async Task Should_StoreRecord()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var store = new InMemoryEventDeliveryLogRepository();
            var record = CreateRecord();

            await store.RecordAsync(record, cancellationToken);

            var result = await store.GetByEventIdAsync(record.Event!.Id, cancellationToken);
            Assert.Single(result);
        }

        [Fact]
        public async Task Should_ThrowArgumentNullException_When_RecordIsNull()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var store = new InMemoryEventDeliveryLogRepository();

            await Assert.ThrowsAsync<ArgumentNullException>(
                () => store.RecordAsync(null!, cancellationToken));
        }

        [Fact]
        public async Task Should_StoreMultipleRecords()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var store = new InMemoryEventDeliveryLogRepository();
            var eventId = Faker.Random.Guid().ToString("N");

            for (var i = 0; i < 5; i++)
            {
                var record = CreateRecord();
                record.Event!.Id = eventId;
                await store.RecordAsync(record, cancellationToken);
            }

            var results = await store.GetByEventIdAsync(eventId, cancellationToken);
            Assert.Equal(5, results.Count);
        }

        [Fact]
        public async Task Should_StoreThroughInterface()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            IEventPublishDeliveryLog store = new InMemoryEventDeliveryLogRepository();
            var record = CreateRecord();

            await store.RecordAsync(record, cancellationToken);

            var queryStore = (IEventDeliveryLogRepository)store;
            var results = await queryStore.GetByEventIdAsync(record.Event!.Id, cancellationToken);
            Assert.Single(results);
        }
    }

    // ── GetByEventIdAsync ───────────────────────────────────────────────────

    public class GetByEventIdAsyncMethod
    {
        [Fact]
        public async Task Should_ReturnRecords_ForGivenEventId()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var store = new InMemoryEventDeliveryLogRepository();
            var eventId = Faker.Random.Guid().ToString("N");

            var record1 = CreateRecord();
            record1.Event!.Id = eventId;
            var record2 = CreateRecord();
            record2.Event!.Id = eventId;
            var other = CreateRecord();

            await store.RecordAsync(record1, cancellationToken);
            await store.RecordAsync(record2, cancellationToken);
            await store.RecordAsync(other, cancellationToken);

            var results = await store.GetByEventIdAsync(eventId, cancellationToken);

            Assert.Equal(2, results.Count);
            Assert.All(results, r => Assert.Equal(eventId, r.Event!.Id));
        }

        [Fact]
        public async Task Should_ReturnRecords_OrderedByTimestamp()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var store = new InMemoryEventDeliveryLogRepository();
            var eventId = Faker.Random.Guid().ToString("N");

            var early = CreateRecord();
            early.Event!.Id = eventId;
            early.Timestamp = DateTimeOffset.UtcNow.AddHours(-2);
            var late = CreateRecord();
            late.Event!.Id = eventId;
            late.Timestamp = DateTimeOffset.UtcNow.AddHours(-1);

            await store.RecordAsync(late, cancellationToken);
            await store.RecordAsync(early, cancellationToken);

            var results = await store.GetByEventIdAsync(eventId, cancellationToken);

            Assert.Equal(2, results.Count);
            Assert.True(results[0].Timestamp <= results[1].Timestamp);
        }

        [Fact]
        public async Task Should_ReturnEmpty_When_NoMatchingEventId()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var store = new InMemoryEventDeliveryLogRepository();
            var record = CreateRecord();
            await store.RecordAsync(record, cancellationToken);

            var results = await store.GetByEventIdAsync("nonexistent", cancellationToken);

            Assert.Empty(results);
        }

        [Fact]
        public async Task Should_ThrowArgumentException_When_EventIdIsNull()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var store = new InMemoryEventDeliveryLogRepository();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => store.GetByEventIdAsync(null!, cancellationToken));
    }

    [Fact]
    public async Task Should_ThrowArgumentException_When_EventIdIsEmpty()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var store = new InMemoryEventDeliveryLogRepository();

            await Assert.ThrowsAsync<ArgumentException>(
                () => store.GetByEventIdAsync("", cancellationToken));
        }
    }

    // ── GetByChannelAsync ───────────────────────────────────────────────────

    public class GetByChannelAsyncMethod
    {
        [Fact]
        public async Task Should_ReturnRecords_ForGivenChannel()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var store = new InMemoryEventDeliveryLogRepository();
            var channel = "orders-channel";

            var record1 = CreateRecord(channelName: channel);
            var record2 = CreateRecord(channelName: channel);
            var other = CreateRecord(channelName: "other-channel");

            await store.RecordAsync(record1, cancellationToken);
            await store.RecordAsync(record2, cancellationToken);
            await store.RecordAsync(other, cancellationToken);

            var results = await store.GetByChannelAsync(channel, cancellationToken);

            Assert.Equal(2, results.Count);
            Assert.All(results, r => Assert.Equal(channel, r.ChannelName));
        }

        [Fact]
        public async Task Should_ReturnRecords_OrderedByTimestampDescending()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var store = new InMemoryEventDeliveryLogRepository();
            var channel = "metrics";

            var early = CreateRecord(channelName: channel);
            early.Timestamp = DateTimeOffset.UtcNow.AddHours(-2);
            var late = CreateRecord(channelName: channel);
            late.Timestamp = DateTimeOffset.UtcNow.AddHours(-1);

            await store.RecordAsync(early, cancellationToken);
            await store.RecordAsync(late, cancellationToken);

            var results = await store.GetByChannelAsync(channel, cancellationToken);

            Assert.True(results[0].Timestamp >= results[1].Timestamp);
        }

        [Fact]
        public async Task Should_ReturnEmpty_When_NoMatchingChannel()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var store = new InMemoryEventDeliveryLogRepository();
            await store.RecordAsync(CreateRecord(), cancellationToken);

            var results = await store.GetByChannelAsync("nonexistent", cancellationToken);

            Assert.Empty(results);
        }

        [Fact]
        public async Task Should_BeCaseInsensitive()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var store = new InMemoryEventDeliveryLogRepository();

            var record = CreateRecord(channelName: "RabbitMQ");
            await store.RecordAsync(record, cancellationToken);

            var results = await store.GetByChannelAsync("rabbitmq", cancellationToken);

            Assert.Single(results);
        }
    }

    // ── GetByOutcomeAsync ───────────────────────────────────────────────────

    public class GetByOutcomeAsyncMethod
    {
        [Fact]
        public async Task Should_ReturnRecords_ForGivenOutcome()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var store = new InMemoryEventDeliveryLogRepository();

            var succeeded = CreateRecord(outcome: EventDeliveryOutcome.Succeeded);
            var failed = CreateRecord(outcome: EventDeliveryOutcome.Failed);

            await store.RecordAsync(succeeded, cancellationToken);
            await store.RecordAsync(failed, cancellationToken);

            var results = await store.GetByOutcomeAsync(EventDeliveryOutcome.Succeeded, cancellationToken);

            Assert.Single(results);
            Assert.Equal(EventDeliveryOutcome.Succeeded, results[0].Outcome);
        }

        [Fact]
        public async Task Should_ReturnAllOutcomeTypes()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var store = new InMemoryEventDeliveryLogRepository();

            await store.RecordAsync(CreateRecord(outcome: EventDeliveryOutcome.Succeeded), cancellationToken);
            await store.RecordAsync(CreateRecord(outcome: EventDeliveryOutcome.Failed), cancellationToken);
            await store.RecordAsync(CreateRecord(outcome: EventDeliveryOutcome.Retried), cancellationToken);

            var succeeded = await store.GetByOutcomeAsync(EventDeliveryOutcome.Succeeded, cancellationToken);
            var failed = await store.GetByOutcomeAsync(EventDeliveryOutcome.Failed, cancellationToken);
            var retried = await store.GetByOutcomeAsync(EventDeliveryOutcome.Retried, cancellationToken);

            Assert.Single(succeeded);
            Assert.Single(failed);
            Assert.Single(retried);
        }

        [Fact]
        public async Task Should_ReturnEmpty_When_NoMatchingOutcome()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var store = new InMemoryEventDeliveryLogRepository();

            var results = await store.GetByOutcomeAsync(EventDeliveryOutcome.Failed, cancellationToken);

            Assert.Empty(results);
        }
    }

    // ── GetByTimeRangeAsync ─────────────────────────────────────────────────

    public class GetByTimeRangeAsyncMethod
    {
        [Fact]
        public async Task Should_ReturnRecords_InTimeRange()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var store = new InMemoryEventDeliveryLogRepository();
            var now = DateTimeOffset.UtcNow;

            var early = CreateRecord();
            early.Timestamp = now.AddHours(-3);
            var middle = CreateRecord();
            middle.Timestamp = now.AddHours(-2);
            var late = CreateRecord();
            late.Timestamp = now.AddHours(-1);

            await store.RecordAsync(early, cancellationToken);
            await store.RecordAsync(middle, cancellationToken);
            await store.RecordAsync(late, cancellationToken);

            var results = await store.GetByTimeRangeAsync(
                now.AddHours(-2.5), now.AddHours(-1.5), cancellationToken);

            Assert.Single(results);
            Assert.Equal(middle.Id, results[0].Id);
        }

        [Fact]
        public async Task Should_ReturnRecords_OrderedByTimestamp()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var store = new InMemoryEventDeliveryLogRepository();
            var from = DateTimeOffset.UtcNow.AddHours(-3);
            var to = DateTimeOffset.UtcNow;

            var first = CreateRecord();
            first.Timestamp = from.AddMinutes(10);
            var second = CreateRecord();
            second.Timestamp = from.AddMinutes(20);

            await store.RecordAsync(second, cancellationToken);
            await store.RecordAsync(first, cancellationToken);

            var results = await store.GetByTimeRangeAsync(from, to, cancellationToken);

            Assert.True(results[0].Timestamp <= results[1].Timestamp);
        }

        [Fact]
        public async Task Should_IncludeBoundaries()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var store = new InMemoryEventDeliveryLogRepository();
            var at = DateTimeOffset.UtcNow;

            var record = CreateRecord();
            record.Timestamp = at;
            await store.RecordAsync(record, cancellationToken);

            var results = await store.GetByTimeRangeAsync(at, at, cancellationToken);

            Assert.Single(results);
        }

        [Fact]
        public async Task Should_ReturnEmpty_When_NoRecordsInRange()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var store = new InMemoryEventDeliveryLogRepository();

            var record = CreateRecord();
            record.Timestamp = DateTimeOffset.UtcNow.AddDays(-1);
            await store.RecordAsync(record, cancellationToken);

            var results = await store.GetByTimeRangeAsync(
                DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow, cancellationToken);

            Assert.Empty(results);
        }
    }

    // ── Storage Backend ─────────────────────────────────────────────────────

    [Fact]
    public void Should_Implement_IStorageBackend()
    {
        var store = new InMemoryEventDeliveryLogRepository();

        Assert.Equal("InMemory", store.ProviderName);
    }

    [Fact]
    public async Task Should_BeThreadSafe_When_ConcurrentlyRecording()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var store = new InMemoryEventDeliveryLogRepository();
        var tasks = new List<Task>();

        for (var i = 0; i < 100; i++)
        {
            var record = CreateRecord();
            tasks.Add(store.RecordAsync(record, cancellationToken));
        }

        await Task.WhenAll(tasks);

        var all = await store.GetByTimeRangeAsync(
            DateTimeOffset.MinValue, DateTimeOffset.MaxValue, cancellationToken);
        Assert.Equal(100, all.Count);
    }
}
