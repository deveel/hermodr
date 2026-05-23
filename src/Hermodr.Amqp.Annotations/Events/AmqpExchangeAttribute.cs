namespace Hermodr
{
    /// <summary>
    /// An attribute that can be used to describe the name of the exchange
    /// that an event should be published to.
    /// </summary>
    public sealed class AmqpExchangeAttribute : EventAttributesAttribute
    {
        /// <summary>
        /// Constructs an attribute with the given exchange name.
        /// </summary>
        /// <param name="exchangeName">
        /// The name of the exchange that the event should be published to.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when the exchange name is <c>null</c>.
        /// </exception>
        public AmqpExchangeAttribute(string exchangeName) 
            : base(AmqpCloudEventAttributes.AmqpExchangeNameAttribute, exchangeName)
        {
            ArgumentNullException.ThrowIfNull(exchangeName, nameof(exchangeName));
        }
    }
}
