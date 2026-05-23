namespace Hermodr
{
    /// <summary>
    /// An attribute to define the routing key to be used when publishing 
    /// an event to an AMQP exchange.
    /// </summary>
    public sealed class AmqpRoutingKeyAttribute : EventAttributesAttribute
    {
        /// <summary>
        /// Constructs the attribute with the routing key to be used.
        /// </summary>
        /// <param name="routingKey">
        /// The AMQP routing key that the event should be routed with when published.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="routingKey"/> is <see langword="null"/>.
        /// </exception>
        public AmqpRoutingKeyAttribute(string routingKey)
            : base(AmqpCloudEventAttributes.AmqpRoutingKeyAttribute, routingKey)
        {
            ArgumentNullException.ThrowIfNull(routingKey, nameof(routingKey));
        }
    }
}
