using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using CloudNative.CloudEvents;

namespace Hermodr;

/// <summary>
/// Represents a record of an event delivery attempt to a publish channel.
/// </summary>
public class EventDeliveryRecord
{
    /// <summary>
    /// Gets or sets the unique identifier of the delivery record.
    /// </summary>
    [Key]
    public string Id { get; set; } = null!;

    /// <summary>
    /// Gets or sets the CloudEvent that was delivered.
    /// </summary>
    [JsonConverter(typeof(CloudEventJsonConverter))]
    public CloudEvent? Event { get; set; }

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
    /// Gets or sets the outcome of the delivery attempt.
    /// </summary>
    public EventDeliveryOutcome Outcome { get; set; }

    /// <summary>
    /// Gets or sets the error code if the delivery failed.
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// Gets or sets the error message if the delivery failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the elapsed time of the delivery attempt.
    /// </summary>
    public TimeSpan ElapsedTime { get; set; }
}
