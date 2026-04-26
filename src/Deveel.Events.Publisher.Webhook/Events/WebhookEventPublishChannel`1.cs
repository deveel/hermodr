//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Deveel.Events
{
    /// <summary>
    /// A <see cref="WebhookEventPublishChannel"/> subclass that also implements
    /// <see cref="IEventPublishChannel{TEvent}"/>, so the <see cref="EventPublisher"/>
    /// routes events of type <typeparamref name="TEvent"/> exclusively to this channel.
    /// </summary>
    /// <remarks>
    /// At construction time the type-specific <see cref="WebhookPublishOptions{TEvent}"/>
    /// are merged with the general <see cref="WebhookPublishOptions"/> (if any) using
    /// <see cref="WebhookPublishOptions.Merge"/>.  Non-<c>null</c> values in the typed
    /// options take precedence; <c>null</c> values fall back to the base defaults.
    /// Channel-structural options are always taken from the base.
    /// </remarks>
    /// <typeparam name="TEvent">
    /// The event data class this channel is keyed against.
    /// </typeparam>
    class WebhookEventPublishChannel<TEvent> :
        WebhookEventPublishChannel,
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
        /// The general <see cref="WebhookPublishOptions"/> registered via
        /// <c>AddWebhooks(configure)</c>.  Unset typed values fall back to these defaults.
        /// </param>
        /// <param name="httpClientFactory">HTTP client factory for webhook delivery.</param>
        /// <param name="signatureProviders">Optional signature providers.</param>
        /// <param name="serializers">Optional event serializers.</param>
        /// <param name="validators">Optional DI-registered options validators.</param>
        /// <param name="logger">Optional logger; falls back to NullLogger when <c>null</c>.</param>
        public WebhookEventPublishChannel(
            IOptions<WebhookPublishOptions<TEvent>> typedOptions,
            IOptions<WebhookPublishOptions> baseOptions,
            IHttpClientFactory httpClientFactory,
            IEnumerable<IWebhookSignatureProvider>? signatureProviders = null,
            IEnumerable<IEventSerializer>? serializers = null,
            IEnumerable<IValidateOptions<WebhookPublishOptions>>? validators = null,
            ILogger<WebhookEventPublishChannel>? logger = null)
            : base(
                Options.Create(WebhookPublishOptions.Merge(baseOptions.Value, typedOptions.Value)),
                httpClientFactory,
                signatureProviders,
                serializers,
                validators,
                logger)
        {
        }
    }
}
