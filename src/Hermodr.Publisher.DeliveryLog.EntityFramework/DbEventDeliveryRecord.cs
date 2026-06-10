using System.ComponentModel.DataAnnotations;

namespace Hermodr;

/// <summary>
/// An Entity Framework entity representing a persisted event delivery record.
/// </summary>
public class DbEventDeliveryRecord
{
    /// <summary>
    /// Gets or sets the unique identifier of the delivery record.
    /// </summary>
    [Key]
    public string Id { get; set; } = null!;

    /// <summary>
    /// Gets or sets the identifier of the published event.
    /// </summary>
    public string EventId { get; set; } = null!;

    /// <summary>
    /// Gets or sets the CloudEvents type of the published event.
    /// </summary>
    public string? EventType { get; set; }

    /// <summary>
    /// Gets or sets the serialized CloudEvent payload.
    /// </summary>
    public string? EventData { get; set; }

    /// <summary>
    /// Gets or sets the name of the publisher pipeline.
    /// </summary>
    public string? PublisherName { get; set; }

    /// <summary>
    /// Gets or sets the logical channel name.
    /// </summary>
    public string? ChannelName { get; set; }

    /// <summary>
    /// Gets or sets the channel type name.
    /// </summary>
    public string? ChannelType { get; set; }

    /// <summary>
    /// Gets or sets the delivery attempt number.
    /// </summary>
    public int AttemptNumber { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp of the delivery attempt.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the delivery outcome string.
    /// </summary>
    public string Outcome { get; set; } = null!;

    /// <summary>
    /// Gets or sets an error code associated with the delivery failure.
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// Gets or sets the error message associated with the delivery failure.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the elapsed time of the delivery attempt in ticks.
    /// </summary>
    public long ElapsedTimeTicks { get; set; }
}
