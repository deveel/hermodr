using Bogus;
using CloudNative.CloudEvents;

namespace Hermodr;

[Trait("Category", "Unit")]
[Trait("Feature", "DeliveryLog")]
public class DbEventDeliveryRecordTests
{
    private static readonly Faker Faker = new("en");

    private static EventDeliveryRecord CreateRecord()
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
            PublisherName = "default",
            ChannelName = "test-channel",
            ChannelType = typeof(string).AssemblyQualifiedName,
            AttemptNumber = 2,
            Timestamp = DateTimeOffset.UtcNow,
            Outcome = EventDeliveryOutcome.Failed,
            ErrorCode = "500",
            ErrorMessage = "Internal error",
            ElapsedTime = TimeSpan.FromMilliseconds(350)
        };
    }

    [Fact]
    public void Should_MapFromRecord_ToEntity()
    {
        var record = CreateRecord();
        var entity = DbEventDeliveryRecord.FromRecord(record);

        Assert.Equal(record.Id, entity.Id);
        Assert.Equal(record.Event!.Id, entity.EventId);
        Assert.Equal(record.Event.Type, entity.EventType);
        Assert.NotNull(entity.EventData);
        Assert.Equal(record.PublisherName, entity.PublisherName);
        Assert.Equal(record.ChannelName, entity.ChannelName);
        Assert.Equal(record.ChannelType, entity.ChannelType);
        Assert.Equal(record.AttemptNumber, entity.AttemptNumber);
        Assert.Equal(record.Timestamp, entity.Timestamp);
        Assert.Equal(record.Outcome.ToString(), entity.Outcome);
        Assert.Equal(record.ErrorCode, entity.ErrorCode);
        Assert.Equal(record.ErrorMessage, entity.ErrorMessage);
        Assert.Equal(record.ElapsedTime.Ticks, entity.ElapsedTimeTicks);
    }

    [Fact]
    public void Should_MapFromEntity_ToRecord()
    {
        var record = CreateRecord();
        var entity = DbEventDeliveryRecord.FromRecord(record);
        var mapped = entity.ToRecord();

        Assert.Equal(record.Id, mapped.Id);
        Assert.NotNull(mapped.Event);
        Assert.Equal(record.Event!.Id, mapped.Event.Id);
        Assert.Equal(record.Event.Type, mapped.Event.Type);
        Assert.Equal(record.PublisherName, mapped.PublisherName);
        Assert.Equal(record.ChannelName, mapped.ChannelName);
        Assert.Equal(record.ChannelType, mapped.ChannelType);
        Assert.Equal(record.AttemptNumber, mapped.AttemptNumber);
        Assert.Equal(record.Timestamp, mapped.Timestamp);
        Assert.Equal(record.Outcome, mapped.Outcome);
        Assert.Equal(record.ErrorCode, mapped.ErrorCode);
        Assert.Equal(record.ErrorMessage, mapped.ErrorMessage);
        Assert.Equal(record.ElapsedTime, mapped.ElapsedTime);
    }

    [Fact]
    public void Should_Roundtrip_AllOutcomes()
    {
        foreach (EventDeliveryOutcome outcome in Enum.GetValues<EventDeliveryOutcome>())
        {
            var record = CreateRecord();
            record.Outcome = outcome;

            var entity = DbEventDeliveryRecord.FromRecord(record);
            var mapped = entity.ToRecord();

            Assert.Equal(outcome, mapped.Outcome);
        }
    }

    [Fact]
    public void Should_Roundtrip_ZeroElapsedTime()
    {
        var record = CreateRecord();
        record.ElapsedTime = TimeSpan.Zero;

        var entity = DbEventDeliveryRecord.FromRecord(record);
        var mapped = entity.ToRecord();

        Assert.Equal(TimeSpan.Zero, mapped.ElapsedTime);
    }

    [Fact]
    public void Should_Roundtrip_MaxElapsedTime()
    {
        var record = CreateRecord();
        record.ElapsedTime = TimeSpan.MaxValue;

        var entity = DbEventDeliveryRecord.FromRecord(record);
        var mapped = entity.ToRecord();

        Assert.Equal(TimeSpan.MaxValue, mapped.ElapsedTime);
    }

    [Fact]
    public void Should_HandleNullProperties()
    {
        var record = new EventDeliveryRecord
        {
            Id = Faker.Random.Guid().ToString("N"),
            Event = new CloudEvent
            {
                Id = Faker.Random.Guid().ToString("N"),
                Type = "test.event",
                Source = new Uri("urn:test")
            },
            Outcome = EventDeliveryOutcome.Succeeded,
            Timestamp = DateTimeOffset.UtcNow,
            ElapsedTime = TimeSpan.Zero
        };

        var entity = DbEventDeliveryRecord.FromRecord(record);
        var mapped = entity.ToRecord();

        Assert.Null(mapped.PublisherName);
        Assert.Null(mapped.ChannelName);
        Assert.Null(mapped.ChannelType);
        Assert.Null(mapped.ErrorCode);
        Assert.Null(mapped.ErrorMessage);
    }

    [Fact]
    public void Should_HandleNullEvent()
    {
        var record = new EventDeliveryRecord
        {
            Id = Faker.Random.Guid().ToString("N"),
            Event = null,
            Outcome = EventDeliveryOutcome.Succeeded,
            Timestamp = DateTimeOffset.UtcNow,
            ElapsedTime = TimeSpan.Zero
        };

        var entity = DbEventDeliveryRecord.FromRecord(record);
        var mapped = entity.ToRecord();

        Assert.Null(mapped.Event);
        Assert.Equal(string.Empty, entity.EventId);
        Assert.Null(entity.EventType);
        Assert.Null(entity.EventData);
    }

    [Fact]
    public void Should_ThrowArgumentNullException_When_RecordIsNull()
    {
        Assert.Throws<ArgumentNullException>(
            () => DbEventDeliveryRecord.FromRecord(null!));
    }
}
