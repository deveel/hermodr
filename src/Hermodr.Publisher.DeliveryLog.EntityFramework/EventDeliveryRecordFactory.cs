using System.Net.Mime;
using System.Text;
using CloudNative.CloudEvents;
using CloudNative.CloudEvents.SystemTextJson;

namespace Hermodr;

public class EventDeliveryRecordFactory : IEventDeliveryRecordFactory<DbEventDeliveryRecord>
{
    private static readonly JsonEventFormatter EventFormatter = new();
    private static readonly ContentType CloudEventsContentType = new("application/cloudevents+json");

    public DbEventDeliveryRecord CreateRecord(EventDeliveryRecord source)
    {
        ArgumentNullException.ThrowIfNull(source);

        string? eventData = null;
        if (source.Event != null)
        {
            var memory = EventFormatter.EncodeStructuredModeMessage(source.Event, out _);
            eventData = Encoding.UTF8.GetString(memory.ToArray());
        }

        return new DbEventDeliveryRecord
        {
            Id = source.Id,
            EventId = source.Event?.Id ?? string.Empty,
            EventType = source.Event?.Type,
            EventData = eventData,
            PublisherName = source.PublisherName,
            ChannelName = source.ChannelName,
            ChannelType = source.ChannelType,
            AttemptNumber = source.AttemptNumber,
            Timestamp = source.Timestamp,
            Outcome = source.Outcome.ToString(),
            ErrorCode = source.ErrorCode,
            ErrorMessage = source.ErrorMessage,
            ElapsedTimeTicks = source.ElapsedTime.Ticks
        };
    }
}
