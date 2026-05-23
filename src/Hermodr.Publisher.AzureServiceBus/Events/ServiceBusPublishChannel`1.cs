//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Azure.Messaging.ServiceBus;

namespace Hermodr
{
    /// <summary>
    /// A <see cref="ServiceBusPublishChannel"/> subclass that also implements
    /// <see cref="IEventPublishChannel{TEvent}"/>, so the <see cref="EventPublisher"/>
    /// routes events of type <typeparamref name="TEvent"/> exclusively to this channel.
    /// </summary>
    /// <remarks>
    /// At construction time the type-specific <see cref="ServiceBusPublishOptions{TEvent}"/>
    /// are merged with the general <see cref="ServiceBusPublishOptions"/> (if any).
    /// Non-empty / non-<c>null</c> typed values take precedence; unset values fall back to the
    /// base defaults.
    /// </remarks>
    /// <typeparam name="TEvent">
    /// The event data class this channel is keyed against.
    /// </typeparam>
    class ServiceBusPublishChannel<TEvent> :
        ServiceBusPublishChannel,
        IEventPublishChannel<TEvent>
        where TEvent : class
    {
        /// <summary>
        /// Constructs the typed channel by merging the general channel options with the
        /// type-specific options, then delegating all publishes to the inherited implementation.
        /// </summary>
        /// <param name="typedOptions">
        /// Type-specific options for <typeparamref name="TEvent"/> events.
        /// Non-<c>null</c> / non-empty properties override the corresponding values from
        /// <paramref name="baseOptions"/>.
        /// </param>
        /// <param name="baseOptions">
        /// The general <see cref="ServiceBusPublishOptions"/> registered via
        /// <c>AddServiceBus(configure)</c>.  When only the typed overload is used these
        /// will contain default values that are overridden by <paramref name="typedOptions"/>.
        /// </param>
        /// <param name="clientFactory">Factory that creates the <see cref="ServiceBusClient"/>.</param>
        /// <param name="messageCreator">Factory that converts a CloudEvent into a Service Bus message.</param>
        /// <param name="validators">Optional DI-registered options validators.</param>
        /// <param name="logger">Optional logger; falls back to NullLogger when <c>null</c>.</param>
        public ServiceBusPublishChannel(
            IOptions<ServiceBusPublishOptions<TEvent>> typedOptions,
            IOptions<ServiceBusPublishOptions> baseOptions,
            IServiceBusClientFactory clientFactory,
            ServiceBusMessageFactory messageCreator,
            IEnumerable<IValidateOptions<ServiceBusPublishOptions>>? validators = null,
            ILogger<ServiceBusPublishChannel>? logger = null)
            : base(
                Options.Create(ServiceBusPublishOptions<TEvent>.Merge(baseOptions.Value, typedOptions.Value)),
                clientFactory,
                messageCreator,
                validators,
                logger)
        {
        }
    }
}
