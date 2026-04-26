//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using MassTransit;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Deveel.Events
{
    /// <summary>
    /// A <see cref="MassTransitPublishChannel"/> subclass that also implements
    /// <see cref="IEventPublishChannel{TEvent}"/>, so the <see cref="EventPublisher"/>
    /// routes events of type <typeparamref name="TEvent"/> exclusively to this channel.
    /// </summary>
    /// <remarks>
    /// At construction time the type-specific <see cref="MassTransitPublishOptions{TEvent}"/>
    /// are merged with the general <see cref="MassTransitPublishOptions"/> (if any) using
    /// <see cref="MassTransitPublishOptions.Merge"/>.  Non-<c>null</c> values in the
    /// typed options take precedence; <c>null</c> values fall back to the base defaults.
    /// </remarks>
    /// <typeparam name="TEvent">
    /// The event data class this channel is keyed against.
    /// </typeparam>
    class MassTransitPublishChannel<TEvent> :
        MassTransitPublishChannel,
        IEventPublishChannel<TEvent>
        where TEvent : class
    {
        /// <summary>
        /// Constructs the typed channel by merging the general channel options with the
        /// type-specific options, then delegating all publishes to the inherited implementation.
        /// </summary>
        /// <param name="typedOptions">
        /// Type-specific options for <typeparamref name="TEvent"/> events.
        /// </param>
        /// <param name="baseOptions">
        /// The general <see cref="MassTransitPublishOptions"/> registered via
        /// <c>AddMassTransit(configure)</c>.  Unset typed values fall back to these defaults.
        /// </param>
        /// <param name="publishEndpoint">MassTransit publish endpoint.</param>
        /// <param name="sendEndpointProvider">MassTransit send-endpoint provider.</param>
        /// <param name="validators">Optional DI-registered options validators.</param>
        /// <param name="logger">Optional logger; falls back to NullLogger when <c>null</c>.</param>
        public MassTransitPublishChannel(
            IOptions<MassTransitPublishOptions<TEvent>> typedOptions,
            IOptions<MassTransitPublishOptions> baseOptions,
            IPublishEndpoint publishEndpoint,
            ISendEndpointProvider sendEndpointProvider,
            IEnumerable<IValidateOptions<MassTransitPublishOptions>>? validators = null,
            ILogger<MassTransitPublishChannel>? logger = null)
            : base(
                Options.Create(MassTransitPublishOptions.Merge(baseOptions.Value, typedOptions.Value)),
                publishEndpoint,
                sendEndpointProvider,
                validators,
                logger)
        {
        }
    }
}
