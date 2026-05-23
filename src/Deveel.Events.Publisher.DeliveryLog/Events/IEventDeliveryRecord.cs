using CloudNative.CloudEvents;

namespace Deveel.Events;

/// <summary>
/// Represents a record of an event delivery attempt to a publish channel.
/// </summary>
public interface IEventDeliveryRecord
{
    /// <summary>
    /// Gets the unique identifier of the delivery record.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets the CloudEvent that was delivered.
    /// </summary>
    CloudEvent? Event { get; }

    /// <summary>
    /// Gets the name of the publisher that sent the event.
    /// </summary>
    string? PublisherName { get; }

    /// <summary>
    /// Gets the name of the channel to which the event was delivered.
    /// </summary>
    string? ChannelName { get; }

    /// <summary>
    /// Gets the type of the channel that was used to deliver the event.
    /// </summary>
    string? ChannelType { get; }

    /// <summary>
    /// Gets the attempt number of the delivery.
    /// </summary>
    int AttemptNumber { get; }

    /// <summary>
    /// Gets the UTC timestamp when the delivery was attempted.
    /// </summary>
    DateTimeOffset Timestamp { get; }

    /// <summary>
    /// Gets the outcome of the delivery attempt.
    /// </summary>
    EventDeliveryOutcome Outcome { get; }

    /// <summary>
    /// Gets the error code if the delivery failed.
    /// </summary>
    string? ErrorCode { get; }

    /// <summary>
    /// Gets the error message if the delivery failed.
    /// </summary>
    string? ErrorMessage { get; }

    /// <summary>
    /// Gets the elapsed time of the delivery attempt.
    /// </summary>
    TimeSpan ElapsedTime { get; }
}
