namespace Hermodr;

/// <summary>
/// Defines the possible outcomes of an event delivery attempt.
/// </summary>
public enum EventDeliveryOutcome
{
    /// <summary>
    /// The event was successfully delivered to the channel.
    /// </summary>
    Succeeded,

    /// <summary>
    /// The event delivery failed and will not be retried.
    /// </summary>
    Failed,

    /// <summary>
    /// The event delivery failed but will be retried.
    /// </summary>
    Retried
}
