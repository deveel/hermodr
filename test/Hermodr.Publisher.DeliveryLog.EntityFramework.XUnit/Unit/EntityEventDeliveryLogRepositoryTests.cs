using Bogus;
using CloudNative.CloudEvents;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hermodr;

[Trait("Category", "Unit")]
[Trait("Feature", "DeliveryLog")]
[Trait("Layer", "Infrastructure")]
public class EntityEventDeliveryLogRepositoryTests : IAsyncDisposable
{
    private static readonly Faker Faker = new("en");
    private readonly SqliteConnection _connection;
    private readonly DeliveryLogDbContext _context;
    private readonly EntityEventDeliveryLogRepository _repository;

    public EntityEventDeliveryLogRepositoryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<DeliveryLogDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new DeliveryLogDbContext(options);
        _context.Database.EnsureCreated();

        _repository = new EntityEventDeliveryLogRepository(_context,
            logger: NullLogger<EntityEventDeliveryLogRepository>.Instance);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private EventDeliveryRecord CreateRecord(string? channelName = null, EventDeliveryOutcome? outcome = null)
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
            AttemptNumber = 1,
            Timestamp = DateTimeOffset.UtcNow.AddMinutes(-Faker.Random.Int(0, 60)),
            Outcome = outcome ?? EventDeliveryOutcome.Succeeded,
            ElapsedTime = TimeSpan.FromMilliseconds(Faker.Random.Int(10, 500))
        };
    }

    [Fact]
    public async Task Should_StoreRecord()
    {
        var record = CreateRecord();
        await _repository.RecordAsync(record, TestContext.Current.CancellationToken);

        var results = await _repository.GetByEventIdAsync(record.Event!.Id, TestContext.Current.CancellationToken);
        Assert.Single(results);
    }

    [Fact]
    public async Task Should_ThrowArgumentNullException_When_RecordIsNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _repository.RecordAsync(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_StoreMultipleRecords()
    {
        var eventId = Guid.NewGuid().ToString("N");

        for (var i = 0; i < 5; i++)
        {
            var record = CreateRecord();
            record.Event!.Id = eventId;
            await _repository.RecordAsync(record, TestContext.Current.CancellationToken);
        }

        var results = await _repository.GetByEventIdAsync(eventId, TestContext.Current.CancellationToken);
        Assert.Equal(5, results.Count);
    }

    [Fact]
    public async Task Should_ReturnRecords_ForGivenEventId()
    {
        var eventId = Guid.NewGuid().ToString("N");

        var record1 = CreateRecord();
        record1.Event!.Id = eventId;
        var record2 = CreateRecord();
        record2.Event!.Id = eventId;
        var other = CreateRecord();

        await _repository.RecordAsync(record1, TestContext.Current.CancellationToken);
        await _repository.RecordAsync(record2, TestContext.Current.CancellationToken);
        await _repository.RecordAsync(other, TestContext.Current.CancellationToken);

        var results = await _repository.GetByEventIdAsync(eventId, TestContext.Current.CancellationToken);
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task Should_ReturnEmpty_When_NoMatchingEventId()
    {
        var record = CreateRecord();
        await _repository.RecordAsync(record, TestContext.Current.CancellationToken);

        var results = await _repository.GetByEventIdAsync("nonexistent", TestContext.Current.CancellationToken);
        Assert.Empty(results);
    }

    [Fact]
    public async Task Should_ThrowOnNullEventId()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _repository.GetByEventIdAsync(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_ThrowOnEmptyEventId()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _repository.GetByEventIdAsync("", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_ReturnRecords_ForGivenChannel()
    {
        var channel = "orders";

        var record1 = CreateRecord(channelName: channel);
        var record2 = CreateRecord(channelName: channel);
        var other = CreateRecord(channelName: "other");

        await _repository.RecordAsync(record1, TestContext.Current.CancellationToken);
        await _repository.RecordAsync(record2, TestContext.Current.CancellationToken);
        await _repository.RecordAsync(other, TestContext.Current.CancellationToken);

        var results = await _repository.GetByChannelAsync(channel, TestContext.Current.CancellationToken);
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task Should_ReturnEmpty_When_NoMatchingChannel()
    {
        await _repository.RecordAsync(CreateRecord(), TestContext.Current.CancellationToken);

        var results = await _repository.GetByChannelAsync("nonexistent", TestContext.Current.CancellationToken);
        Assert.Empty(results);
    }

    [Fact]
    public async Task Should_ThrowOnNullChannel()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _repository.GetByChannelAsync(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_ReturnRecords_ForGivenOutcome()
    {
        await _repository.RecordAsync(CreateRecord(outcome: EventDeliveryOutcome.Succeeded), TestContext.Current.CancellationToken);
        var failed = CreateRecord(outcome: EventDeliveryOutcome.Failed);
        await _repository.RecordAsync(failed, TestContext.Current.CancellationToken);

        var results = await _repository.GetByOutcomeAsync(EventDeliveryOutcome.Failed, TestContext.Current.CancellationToken);
        Assert.Single(results);
        Assert.Equal(EventDeliveryOutcome.Failed, results[0].Outcome);
    }

    [Fact]
    public async Task Should_ReturnEmpty_When_NoMatchingOutcome()
    {
        await _repository.RecordAsync(CreateRecord(outcome: EventDeliveryOutcome.Succeeded), TestContext.Current.CancellationToken);

        var results = await _repository.GetByOutcomeAsync(EventDeliveryOutcome.Failed, TestContext.Current.CancellationToken);
        Assert.Empty(results);
    }

    [Fact]
    public async Task Should_ReturnRecords_InTimeRange()
    {
        var now = DateTimeOffset.UtcNow;

        var early = CreateRecord();
        early.Timestamp = now.AddHours(-3);
        var middle = CreateRecord();
        middle.Timestamp = now.AddHours(-2);
        var late = CreateRecord();
        late.Timestamp = now.AddHours(-1);

        await _repository.RecordAsync(early, TestContext.Current.CancellationToken);
        await _repository.RecordAsync(middle, TestContext.Current.CancellationToken);
        await _repository.RecordAsync(late, TestContext.Current.CancellationToken);

        var results = await _repository.GetByTimeRangeAsync(
            now.AddHours(-2.5), now.AddHours(-1.5), TestContext.Current.CancellationToken);
        Assert.Single(results);
        Assert.Equal(middle.Id, results[0].Id);
    }

    [Fact]
    public async Task Should_ReturnEmpty_When_NoRecordsInRange()
    {
        await _repository.RecordAsync(CreateRecord(), TestContext.Current.CancellationToken);

        var results = await _repository.GetByTimeRangeAsync(
            DateTimeOffset.UtcNow.AddDays(-2), DateTimeOffset.UtcNow.AddDays(-1),
            TestContext.Current.CancellationToken);
        Assert.Empty(results);
    }

    [Fact]
    public void Should_Implement_IStorageBackend()
    {
        Assert.Equal("EntityFrameworkCore", _repository.ProviderName);
    }

    [Fact]
    public void Should_CreateDatabaseSchema()
    {
        var created = _context.Database.EnsureCreated();
        Assert.False(created);

        var all = _context.DeliveryRecords.ToList();
        Assert.Empty(all);
    }
}
