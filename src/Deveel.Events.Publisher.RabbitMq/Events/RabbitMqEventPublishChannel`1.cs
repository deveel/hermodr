//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using RabbitMQ.Client;

namespace Deveel.Events
{
    /// <summary>
    /// A <see cref="RabbitMqEventPublishChannel"/> subclass that also implements
    /// <see cref="IEventPublishChannel{TEvent}"/>, so the <see cref="EventPublisher"/>
    /// routes events of type <typeparamref name="TEvent"/> exclusively to this channel.
    /// </summary>
    /// <remarks>
    /// At construction time the type-specific <see cref="RabbitMqPublishOptions{TEvent}"/>
    /// are merged with the general <see cref="RabbitMqPublishOptions"/> (if any) using
    /// <see cref="RabbitMqPublishOptions.Merge"/>.  Non-<c>null</c> values in the
    /// typed options take precedence; <c>null</c> values fall back to the base defaults.
    /// </remarks>
    /// <typeparam name="TEvent">
    /// The event data class this channel is keyed against.
    /// </typeparam>
    class RabbitMqEventPublishChannel<TEvent> :
        RabbitMqEventPublishChannel,
        IEventPublishChannel<TEvent>
        where TEvent : class
    {
        /// <summary>
        /// Constructs the typed channel by merging the general channel options with the
        /// type-specific options, then delegating all publishes to the inherited implementation.
        /// </summary>
        /// <param name="typedOptions">
        /// Type-specific options for <typeparamref name="TEvent"/> events.
        /// Non-<c>null</c> properties override the corresponding values from
        /// <paramref name="baseOptions"/>.
        /// </param>
        /// <param name="baseOptions">
        /// The general <see cref="RabbitMqPublishOptions"/> registered via
        /// <c>AddRabbitMq(configure)</c>.  When only the typed overload is used these
        /// will contain default values that are overridden by <paramref name="typedOptions"/>.
        /// </param>
        /// <param name="connection">The AMQ connection shared with other channels.</param>
        /// <param name="messageFactory">Factory that converts a CloudEvent into a RabbitMQ message.</param>
        /// <param name="validators">Optional DI-registered options validators.</param>
        /// <param name="logger">Optional logger; falls back to NullLogger when <c>null</c>.</param>
        public RabbitMqEventPublishChannel(
            IOptions<RabbitMqPublishOptions<TEvent>> typedOptions,
            IOptions<RabbitMqPublishOptions> baseOptions,
            IConnection connection,
            IRabbitMqMessageFactory messageFactory,
            IEnumerable<IValidateOptions<RabbitMqPublishOptions>>? validators = null,
            ILogger<RabbitMqEventPublishChannel>? logger = null)
            : base(
                Options.Create(RabbitMqPublishOptions.Merge(baseOptions.Value, typedOptions.Value)),
                connection,
                messageFactory,
                validators,
                logger)
        {
        }
    }
}
