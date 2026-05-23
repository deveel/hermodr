namespace Hermodr
{
    /// <summary>
    /// The MassTransit message contract used to wrap a structured-mode
    /// CloudEvent payload for publishing via <see cref="MassTransitPublishChannel"/>.
    /// </summary>
    /// <remarks>
    /// MassTransit uses this interface as the message type when calling
    /// <c>IPublishEndpoint.Publish</c> or <c>ISendEndpoint.Send</c>, so that
    /// consumers can subscribe by message type.
    /// </remarks>
    public interface ICloudEventMessage
    {
        /// <summary>
        /// The raw bytes of the structured-mode CloudEvent, typically encoded
        /// as <c>application/cloudevents+json</c>.
        /// </summary>
        byte[] Body { get; }

        /// <summary>
        /// The MIME content-type of <see cref="Body"/>,
        /// e.g. <c>application/cloudevents+json</c>.
        /// </summary>
        string ContentType { get; }
    }
}
