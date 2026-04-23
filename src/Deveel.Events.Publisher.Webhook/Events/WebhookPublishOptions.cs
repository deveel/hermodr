//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Deveel.Events
{
    /// <summary>
    /// Per-delivery options that override the defaults configured in
    /// <see cref="WebhookEventPublishChannelOptions"/> for a single publish call.
    /// </summary>
    /// <remarks>
    /// Only non-<c>null</c> properties replace the channel default; any property
    /// left <c>null</c> retains the channel-level configuration.
    /// </remarks>
    public class WebhookPublishOptions
    {
        /// <summary>Override the destination URL for this delivery only.</summary>
        public string? EndpointUrl { get; set; }

        /// <summary>
        /// Override the HMAC signing secret for this delivery only.
        /// Set to <see cref="string.Empty"/> to suppress the signature headers
        /// for this message while keeping the channel default configured.
        /// </summary>
        public string? SigningSecret { get; set; }

        /// <summary>Override the maximum number of retry attempts for this delivery.</summary>
        public int? MaxRetryCount { get; set; }

        /// <summary>Override the initial retry delay for this delivery.</summary>
        public TimeSpan? RetryDelay { get; set; }

        /// <summary>Override the exponential-backoff multiplier for this delivery.</summary>
        public double? RetryBackoffMultiplier { get; set; }

        /// <summary>Override the per-request HTTP timeout for this delivery.</summary>
        public TimeSpan? RequestTimeout { get; set; }

        /// <summary>
        /// Override the serialization format for this delivery.
        /// Must match the <see cref="IEventSerializer.Format"/> of a
        /// registered serializer. Use constants from <see cref="WebhookMessageFormat"/>
        /// or any string identifying a custom-registered serializer.
        /// </summary>
        public string? MessageFormat { get; set; }

        /// <summary>Override the HMAC signing algorithm for this delivery.</summary>
        public WebhookSignatureAlgorithm? SignatureAlgorithm { get; set; }

        /// <summary>
        /// Additional headers to merge into (and override) the channel-level
        /// <see cref="WebhookEventPublishChannelOptions.AdditionalHeaders"/>
        /// for this delivery only.
        /// </summary>
        public IDictionary<string, string>? AdditionalHeaders { get; set; }
    }
}
