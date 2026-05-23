using Bogus;
using CloudNative.CloudEvents;

namespace Deveel.Events;

[Trait("Category", "Unit")]
[Trait("Feature", "DeliveryLog")]
public class EventDeliveryRecordTests
{
    private static readonly Faker Faker = new("en");

    [Fact]
    public void Should_CreateRecord_With_DefaultValues()
    {
        var record = new EventDeliveryRecord();

        Assert.Null(record.Id);
        Assert.Null(record.Event);
        Assert.Null(record.ChannelName);
        Assert.Equal(0, record.AttemptNumber);
        Assert.Equal(default, record.Timestamp);
        Assert.Equal(default, record.Outcome);
        Assert.Null(record.ErrorCode);
        Assert.Null(record.ErrorMessage);
        Assert.Equal(TimeSpan.Zero, record.ElapsedTime);
    }

    [Fact]
    public void Should_SetAllProperties()
    {
        var id = Faker.Random.Guid().ToString("N");
        var eventId = Faker.Random.Guid().ToString("N");
        var eventType = "test.event";
        var cloudEvent = new CloudEvent
        {
            Id = eventId,
            Type = eventType,
            Source = new Uri("urn:test"),
            Time = DateTimeOffset.UtcNow
        };
        var publisherName = "default";
        var channelName = "rabbitmq";
        var channelType = typeof(string).AssemblyQualifiedName;
        var attempt = 3;
        var timestamp = DateTimeOffset.UtcNow;
        var elapsed = TimeSpan.FromMilliseconds(245);

        var record = new EventDeliveryRecord
        {
            Id = id,
            Event = cloudEvent,
            PublisherName = publisherName,
            ChannelName = channelName,
            ChannelType = channelType,
            AttemptNumber = attempt,
            Timestamp = timestamp,
            Outcome = EventDeliveryOutcome.Failed,
            ErrorCode = "500",
            ErrorMessage = "Internal server error",
            ElapsedTime = elapsed
        };

        Assert.Equal(id, record.Id);
        Assert.Same(cloudEvent, record.Event);
        Assert.Equal(eventId, record.Event.Id);
        Assert.Equal(eventType, record.Event.Type);
        Assert.Equal(publisherName, record.PublisherName);
        Assert.Equal(channelName, record.ChannelName);
        Assert.Equal(channelType, record.ChannelType);
        Assert.Equal(attempt, record.AttemptNumber);
        Assert.Equal(timestamp, record.Timestamp);
        Assert.Equal(EventDeliveryOutcome.Failed, record.Outcome);
        Assert.Equal("500", record.ErrorCode);
        Assert.Equal("Internal server error", record.ErrorMessage);
        Assert.Equal(elapsed, record.ElapsedTime);
    }

    [Fact]
    public void Should_SetSucceededOutcome()
    {
        var record = new EventDeliveryRecord
        {
            Outcome = EventDeliveryOutcome.Succeeded
        };

        Assert.Equal(EventDeliveryOutcome.Succeeded, record.Outcome);
    }

    [Fact]
    public void Should_SetFailedOutcome()
    {
        var record = new EventDeliveryRecord
        {
            Outcome = EventDeliveryOutcome.Failed
        };

        Assert.Equal(EventDeliveryOutcome.Failed, record.Outcome);
    }

    [Fact]
    public void Should_SetRetriedOutcome()
    {
        var record = new EventDeliveryRecord
        {
            Outcome = EventDeliveryOutcome.Retried
        };

        Assert.Equal(EventDeliveryOutcome.Retried, record.Outcome);
    }

    [Fact]
    public void Should_SetElapsedTime()
    {
        var expected = TimeSpan.FromSeconds(1.5);
        var record = new EventDeliveryRecord
        {
            ElapsedTime = expected
        };

        Assert.Equal(expected, record.ElapsedTime);
    }
}
