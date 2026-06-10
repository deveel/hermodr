using Bogus;
using CloudNative.CloudEvents;

namespace Hermodr;

[Trait("Category", "Unit")]
[Trait("Feature", "DeliveryLog")]
public class DbEventDeliveryRecordTests
{
    private static readonly Faker Faker = new("en");
    private static readonly EventDeliveryRecordFactory Factory = new();

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
        var entity = Factory.CreateRecord(record);

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

        var entity = Factory.CreateRecord(record);

        Assert.Null(entity.PublisherName);
        Assert.Null(entity.ChannelName);
        Assert.Null(entity.ChannelType);
        Assert.Null(entity.ErrorCode);
        Assert.Null(entity.ErrorMessage);
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

        var entity = Factory.CreateRecord(record);

        Assert.Null(entity.EventData);
        Assert.Equal(string.Empty, entity.EventId);
        Assert.Null(entity.EventType);
    }

    [Fact]
    public void Should_ThrowArgumentNullException_When_RecordIsNull()
    {
        Assert.Throws<ArgumentNullException>(
            () => Factory.CreateRecord(null!));
    }
}
