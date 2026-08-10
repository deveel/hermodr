//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Dapr.Client;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hermodr
{
    /// <summary>
    /// A <see cref="DaprPublishChannel"/> subclass that also implements
    /// <see cref="IEventPublishChannel{TEvent}"/>, so the <see cref="EventPublisher"/>
    /// routes events of type <typeparamref name="TEvent"/> exclusively to this channel.
    /// </summary>
    /// <remarks>
    /// At construction time the type-specific <see cref="DaprPublishOptions{TEvent}"/>
    /// are merged with the general <see cref="DaprPublishOptions"/> (if any) using
    /// <see cref="DaprPublishOptions.Merge"/>.  Non-<c>null</c> values in the
    /// typed options take precedence; <c>null</c> values fall back to the base defaults.
    /// </remarks>
    /// <typeparam name="TEvent">
    /// The event data class this channel is keyed against.
    /// </typeparam>
    internal class DaprPublishChannel<TEvent> :
        DaprPublishChannel,
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
        /// The general <see cref="DaprPublishOptions"/> registered via
        /// <c>AddDapr(configure)</c>.  Unset typed values fall back to these defaults.
        /// </param>
        /// <param name="clientBuilder">The Dapr client builder.</param>
        /// <param name="serviceProvider">The service provider for validators.</param>
        /// <param name="logger">Optional logger; falls back to NullLogger when <c>null</c>.</param>
        public DaprPublishChannel(
            IOptions<DaprPublishOptions<TEvent>> typedOptions,
            IOptions<DaprPublishOptions> baseOptions,
            IDaprClientBuilder clientBuilder,
            IServiceProvider serviceProvider,
            ILogger<DaprPublishChannel>? logger = null)
            : base(
                Options.Create(DaprPublishOptions.Merge(baseOptions.Value, typedOptions.Value)),
                clientBuilder,
                serviceProvider,
                logger)
        {
        }
    }
}