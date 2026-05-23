using System.ComponentModel.DataAnnotations;
using System.Net.Mime;
using System.Text;
using CloudNative.CloudEvents;
using CloudNative.CloudEvents.SystemTextJson;

namespace Deveel.Events;

/// <summary>
/// An Entity Framework Core entity that represents an event delivery record,
/// implementing <see cref="IEventDeliveryRecord"/>.
/// </summary>
public class DbEventDeliveryRecord : IEventDeliveryRecord
{
    private static readonly JsonEventFormatter EventFormatter = new();
    private static readonly ContentType CloudEventsContentType = new("application/cloudevents+json");

    /// <summary>
    /// Gets or sets the unique identifier of the delivery record.
    /// </summary>
    [Key]
    public string Id { get; set; } = null!;

    /// <summary>
    /// Gets or sets the identifier of the delivered event.
    /// </summary>
    public string EventId { get; set; } = null!;

    /// <summary>
    /// Gets or sets the type of the delivered event.
    /// </summary>
    public string? EventType { get; set; }

    /// <summary>
    /// Gets or sets the serialized JSON data of the CloudEvent.
    /// </summary>
    public string? EventData { get; set; }

    /// <summary>
    /// Gets or sets the name of the publisher that sent the event.
    /// </summary>
    public string? PublisherName { get; set; }

    /// <summary>
    /// Gets or sets the name of the channel to which the event was delivered.
    /// </summary>
    public string? ChannelName { get; set; }

    /// <summary>
    /// Gets or sets the type of the channel that was used to deliver the event.
    /// </summary>
    public string? ChannelType { get; set; }

    /// <summary>
    /// Gets or sets the attempt number of the delivery.
    /// </summary>
    public int AttemptNumber { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the delivery was attempted.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the outcome of the delivery attempt as a string value.
    /// </summary>
    public string Outcome { get; set; } = null!;
    
    EventDeliveryOutcome IEventDeliveryRecord.Outcome 
        => Enum.TryParse<EventDeliveryOutcome>(Outcome, true, out var outcome) ? outcome : EventDeliveryOutcome.Failed;
    
    /// <summary>
    /// Gets or sets the error code if the delivery failed.
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// Gets or sets the error message if the delivery failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the elapsed time of the delivery attempt in ticks.
    /// </summary>
    public long ElapsedTimeTicks { get; set; }
    
    TimeSpan IEventDeliveryRecord.ElapsedTime => TimeSpan.FromTicks(ElapsedTimeTicks);

    CloudEvent? IEventDeliveryRecord.Event
    {
        get
        {
            if (EventData != null)
            {
                try
                {
                    var bytes = Encoding.UTF8.GetBytes(EventData);
                    return EventFormatter.DecodeStructuredModeMessage(bytes, CloudEventsContentType, null);
                }
                catch (ArgumentException)
                {
                    // ignore deserialization errors
                }
                catch (InvalidOperationException)
                {
                    // ignore deserialization errors
                }
            }

            return null;
        }
    }

    /// <summary>
    /// Converts this entity to an <see cref="EventDeliveryRecord"/>.
    /// </summary>
    /// <returns>
    /// An <see cref="EventDeliveryRecord"/> populated with the values from this entity.
    /// </returns>
    public EventDeliveryRecord ToRecord()
    {
        CloudEvent? cloudEvent = null;
        if (EventData != null)
        {
            try
            {
                var bytes = Encoding.UTF8.GetBytes(EventData);
                cloudEvent = EventFormatter.DecodeStructuredModeMessage(bytes, CloudEventsContentType, null);
            }
            catch (ArgumentException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }

        return new EventDeliveryRecord
        {
            Id = Id,
            Event = cloudEvent,
            PublisherName = PublisherName,
            ChannelName = ChannelName,
            ChannelType = ChannelType,
            AttemptNumber = AttemptNumber,
            Timestamp = Timestamp,
            Outcome = Enum.TryParse<EventDeliveryOutcome>(Outcome, true, out var outcome) 
                ? outcome : EventDeliveryOutcome.Failed,
            ErrorCode = ErrorCode,
            ErrorMessage = ErrorMessage,
            ElapsedTime = TimeSpan.FromTicks(ElapsedTimeTicks)
        };
    }

    /// <summary>
    /// Creates a new <see cref="DbEventDeliveryRecord"/> from an <see cref="IEventDeliveryRecord"/>.
    /// </summary>
    /// <param name="record">
    /// The source record to copy from.
    /// </param>
    /// <returns>
    /// A new <see cref="DbEventDeliveryRecord"/> populated with the values from the source.
    /// </returns>
    public static DbEventDeliveryRecord FromRecord(IEventDeliveryRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        string? eventData = null;
        if (record.Event != null)
        {
            var memory = EventFormatter.EncodeStructuredModeMessage(record.Event, out _);
            eventData = Encoding.UTF8.GetString(memory.ToArray());
        }

        return new DbEventDeliveryRecord
        {
            Id = record.Id,
            EventId = record.Event?.Id ?? string.Empty,
            EventType = record.Event?.Type,
            EventData = eventData,
            PublisherName = record.PublisherName,
            ChannelName = record.ChannelName,
            ChannelType = record.ChannelType,
            AttemptNumber = record.AttemptNumber,
            Timestamp = record.Timestamp,
            Outcome = record.Outcome.ToString(),
            ErrorCode = record.ErrorCode,
            ErrorMessage = record.ErrorMessage,
            ElapsedTimeTicks = record.ElapsedTime.Ticks
        };
    }
}
