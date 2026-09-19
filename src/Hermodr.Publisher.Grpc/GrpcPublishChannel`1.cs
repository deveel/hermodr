//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hermodr
{
    /// <summary>
    /// A <see cref="GrpcPublishChannel"/> subclass that also implements
    /// <see cref="IEventPublishChannel{TEvent}"/>, so the <see cref="EventPublisher"/>
    /// routes events of type <typeparamref name="TEvent"/> exclusively to this channel.
    /// </summary>
    /// <remarks>
    /// At construction time the type-specific <see cref="GrpcPublishOptions{TEvent}"/>
    /// are merged with the general <see cref="GrpcPublishOptions"/> (if any) using
    /// <see cref="GrpcPublishOptions.Merge"/>. Non-<c>null</c> values in the typed
    /// options take precedence; <c>null</c> values fall back to the base defaults.
    /// </remarks>
    /// <typeparam name="TEvent">
    /// The event data class this channel is keyed against.
    /// </typeparam>
    internal class GrpcPublishChannel<TEvent> :
        GrpcPublishChannel,
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
        /// The general <see cref="GrpcPublishOptions"/> registered via
        /// <c>AddGrpcEventPublisherChannel(configure)</c>. Unset typed values fall back to
        /// these defaults.
        /// </param>
        /// <param name="channelFactory">The gRPC channel factory for endpoint delivery.</param>
        /// <param name="defaultSender">The default event sender strategy.</param>
        /// <param name="serviceProvider">The service provider used to lazily resolve dependencies.</param>
        /// <param name="logger">Optional logger; falls back to NullLogger when <c>null</c>.</param>
        public GrpcPublishChannel(
            IOptions<GrpcPublishOptions<TEvent>> typedOptions,
            IOptions<GrpcPublishOptions> baseOptions,
            IGrpcChannelFactory channelFactory,
            IGrpcEventSender defaultSender,
            IServiceProvider serviceProvider,
            ILogger<GrpcPublishChannel>? logger = null)
            : base(
                Options.Create(GrpcPublishOptions.Merge(baseOptions.Value, typedOptions.Value)),
                channelFactory,
                defaultSender,
                serviceProvider,
                logger)
        {
        }
    }
}
