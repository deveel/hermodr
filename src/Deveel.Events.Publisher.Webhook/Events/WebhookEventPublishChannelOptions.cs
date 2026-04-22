//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Deveel.Events
{
    /// <summary>
    /// Configuration options for a <see cref="WebhookEventPublishChannel"/>.
    /// </summary>
    public class WebhookEventPublishChannelOptions
    {
        /// <summary>
        /// The URL of the webhook endpoint to deliver events to.
        /// </summary>
        public string EndpointUrl { get; set; } = string.Empty;

        /// <summary>
        /// The shared secret used to compute the HMAC signature.
        /// When <c>null</c> or empty, no signature headers are sent.
        /// </summary>
        public string? SigningSecret { get; set; }

        /// <summary>
        /// The name of the HTTP header that carries the HMAC signature.
        /// Defaults to <c>X-Webhook-Signature</c>.
        /// </summary>
        public string SignatureHeaderName { get; set; } = WebhookDefaults.SignatureHeaderName;

        /// <summary>
        /// The name of the HTTP header that carries the unique delivery identifier.
        /// Defaults to <c>X-Webhook-Delivery</c>.
        /// </summary>
        public string DeliveryIdHeaderName { get; set; } = WebhookDefaults.DeliveryIdHeaderName;

        /// <summary>
        /// The name of the HTTP header that carries the event type.
        /// Defaults to <c>X-Webhook-Event</c>.
        /// </summary>
        public string EventTypeHeaderName { get; set; } = WebhookDefaults.EventTypeHeaderName;

        /// <summary>
        /// The HTTP header that carries the Unix timestamp (seconds since epoch)
        /// of the delivery, included in the HMAC signature to prevent replay attacks.
        /// Defaults to <c>X-Webhook-Timestamp</c>.
        /// </summary>
        public string TimestampHeaderName { get; set; } = WebhookDefaults.TimestampHeaderName;

        /// <summary>
        /// Additional custom headers to include in every webhook request.
        /// </summary>
        public IDictionary<string, string> AdditionalHeaders { get; set; }
            = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Maximum number of delivery attempts before giving up.
        /// Set to <c>0</c> to disable retries. Defaults to <c>3</c>.
        /// </summary>
        public int MaxRetryCount { get; set; } = 3;

        /// <summary>
        /// The initial delay between retry attempts. Defaults to 1 second.
        /// Subsequent delays are multiplied by <see cref="RetryBackoffMultiplier"/>.
        /// </summary>
        public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Exponential-backoff multiplier applied to <see cref="RetryDelay"/>
        /// on each consecutive retry. Defaults to <c>2.0</c>.
        /// </summary>
        public double RetryBackoffMultiplier { get; set; } = 2.0;

        /// <summary>
        /// HTTP status codes that are considered transient and eligible for a retry.
        /// Defaults to 429, 500, 502, 503 and 504.
        /// </summary>
        public ISet<int> RetryableStatusCodes { get; set; }
            = new HashSet<int> { 429, 500, 502, 503, 504 };

        /// <summary>
        /// The timeout for each individual HTTP request. Defaults to 30 seconds.
        /// </summary>
        public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// The name of the named <see cref="System.Net.Http.HttpClient"/> to resolve
        /// from <see cref="System.Net.Http.IHttpClientFactory"/>. When <c>null</c>
        /// the default channel name (<see cref="WebhookDefaults.HttpClientName"/>) is used.
        /// </summary>
        public string? HttpClientName { get; set; }

        /// <summary>
        /// The serialization format for the request body.
        /// Must match the <see cref="IEventSerializer.Format"/> of a registered
        /// serializer. Use constants from <see cref="WebhookMessageFormat"/> or supply
        /// any string that identifies a custom-registered serializer.
        /// Defaults to <see cref="WebhookMessageFormat.Json"/> (plain JSON).
        /// Can be overridden per delivery via <see cref="WebhookPublishOptions.MessageFormat"/>.
        /// </summary>
        public string MessageFormat { get; set; } = WebhookMessageFormat.Json;

        /// <summary>
        /// The HMAC algorithm used to sign request bodies.
        /// Defaults to <see cref="WebhookSignatureAlgorithm.HmacSha256"/>.
        /// Can be overridden per delivery via <see cref="WebhookPublishOptions.SignatureAlgorithm"/>.
        /// </summary>
        public WebhookSignatureAlgorithm SignatureAlgorithm { get; set; }
            = WebhookSignatureAlgorithm.HmacSha256;

        /// <summary>
        /// The name of the HTTP header that communicates which signing algorithm was
        /// used, so receivers can validate with the same algorithm.
        /// Defaults to <c>X-Webhook-Signature-Algorithm</c>.
        /// Set to <c>null</c> or empty to suppress this header.
        /// </summary>
        public string? SignatureAlgorithmHeaderName { get; set; }
            = WebhookDefaults.SignatureAlgorithmHeaderName;
    }
}
