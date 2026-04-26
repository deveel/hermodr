//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.ComponentModel.DataAnnotations;

namespace Deveel.Events
{
    /// <summary>
    /// Options for the <see cref="WebhookEventPublishChannel"/>, used both as the
    /// channel-level configuration (injected via <see cref="Microsoft.Extensions.Options.IOptions{TOptions}"/>)
    /// and as per-delivery overrides passed directly to
    /// <see cref="IEventPublishChannel{TOptions}.PublishAsync"/>.
    /// </summary>
    /// <remarks>
    /// When used as a per-delivery override, only non-<c>null</c> properties replace
    /// the channel default; any nullable property left <c>null</c> retains the
    /// channel-level configuration.  The channel-structural properties
    /// (<see cref="SignatureHeaderName"/>, <see cref="DeliveryIdHeaderName"/>, etc.)
    /// are always taken from the channel-level defaults and ignored when supplied
    /// in a per-call override.
    /// </remarks>
    public class WebhookPublishOptions : EventPublishOptions, INamedChannelFilter
    {
        /// <summary>
        /// Merges <paramref name="baseOptions"/> with <paramref name="typedOptions"/>,
        /// where every non-<c>null</c> delivery property in <paramref name="typedOptions"/>
        /// overrides the corresponding property from <paramref name="baseOptions"/>.
        /// Additional headers are merged (typed entries win on key collision).
        /// Channel-structural fields are always taken from <paramref name="baseOptions"/>.
        /// </summary>
        public static WebhookPublishOptions Merge(
            WebhookPublishOptions baseOptions,
            WebhookPublishOptions typedOptions)
        {
            var mergedHeaders = new Dictionary<string, string>(
                baseOptions.AdditionalHeaders, StringComparer.OrdinalIgnoreCase);
            foreach (var kv in typedOptions.AdditionalHeaders)
                mergedHeaders[kv.Key] = kv.Value;

            return new WebhookPublishOptions
            {
                ChannelName            = typedOptions.ChannelName            ?? baseOptions.ChannelName,
                EndpointUrl            = typedOptions.EndpointUrl            ?? baseOptions.EndpointUrl,
                SigningSecret          = typedOptions.SigningSecret          ?? baseOptions.SigningSecret,
                MaxRetryCount          = typedOptions.MaxRetryCount          ?? baseOptions.MaxRetryCount,
                RetryDelay             = typedOptions.RetryDelay             ?? baseOptions.RetryDelay,
                RetryBackoffMultiplier = typedOptions.RetryBackoffMultiplier ?? baseOptions.RetryBackoffMultiplier,
                RequestTimeout         = typedOptions.RequestTimeout         ?? baseOptions.RequestTimeout,
                MessageFormat          = typedOptions.MessageFormat          ?? baseOptions.MessageFormat,
                SignatureAlgorithm     = typedOptions.SignatureAlgorithm     ?? baseOptions.SignatureAlgorithm,
                AdditionalHeaders      = mergedHeaders,
                // Channel-structural — always from base
                SignatureHeaderName         = baseOptions.SignatureHeaderName,
                DeliveryIdHeaderName        = baseOptions.DeliveryIdHeaderName,
                EventTypeHeaderName         = baseOptions.EventTypeHeaderName,
                TimestampHeaderName         = baseOptions.TimestampHeaderName,
                SignatureAlgorithmHeaderName = baseOptions.SignatureAlgorithmHeaderName,
                RetryableStatusCodes        = baseOptions.RetryableStatusCodes,
                HttpClientName              = baseOptions.HttpClientName,
            };
        }

        /// <inheritdoc/>
        public string? ChannelName { get; set; }


        // ── Delivery settings — nullable so per-call overrides can be partial ──

        /// <summary>
        /// The URL of the webhook endpoint.
        /// Required in the effective (merged) options; may be <c>null</c> when
        /// used as a per-call override that inherits the endpoint from the channel.
        /// </summary>
        [Required(AllowEmptyStrings = false,
            ErrorMessage = "The effective webhook EndpointUrl is required and must not be empty.")]
        [Url(ErrorMessage = "The EndpointUrl must be a valid absolute URL.")]
        public string? EndpointUrl { get; set; }

        /// <summary>
        /// The shared HMAC signing secret.
        /// When <c>null</c> (per-call context) the channel secret is used.
        /// Set to <see cref="string.Empty"/> to suppress signature headers for this delivery.
        /// </summary>
        public string? SigningSecret { get; set; }

        /// <summary>
        /// Maximum number of delivery attempts before giving up.
        /// Set to <c>0</c> to disable retries. When <c>null</c> the channel default
        /// (<c>3</c>) is used.
        /// </summary>
        public int? MaxRetryCount { get; set; }

        /// <summary>
        /// The initial delay between retry attempts.
        /// When <c>null</c> the channel default (<c>1 s</c>) is used.
        /// Subsequent delays are multiplied by <see cref="RetryBackoffMultiplier"/>.
        /// </summary>
        public TimeSpan? RetryDelay { get; set; }

        /// <summary>
        /// Exponential-backoff multiplier applied to <see cref="RetryDelay"/>
        /// on each consecutive retry. When <c>null</c> the channel default (<c>2.0</c>)
        /// is used.
        /// </summary>
        public double? RetryBackoffMultiplier { get; set; }

        /// <summary>
        /// The timeout for each individual HTTP request.
        /// When <c>null</c> the channel default (<c>30 s</c>) is used.
        /// </summary>
        public TimeSpan? RequestTimeout { get; set; }

        /// <summary>
        /// The serialization format for the request body.
        /// Defaults to <see cref="EventMessageFormat.Json"/> when <c>null</c>.
        /// Must match the <see cref="IEventSerializer.Format"/> of a registered serializer.
        /// </summary>
        public string? MessageFormat { get; set; }

        /// <summary>
        /// The HMAC algorithm used to sign request bodies.
        /// Defaults to <see cref="WebhookSignatureAlgorithm.HmacSha256"/> when <c>null</c>.
        /// </summary>
        public WebhookSignatureAlgorithm? SignatureAlgorithm { get; set; }

        /// <summary>
        /// Additional HTTP headers merged into every webhook request.
        /// In a per-call override, these are merged on top of the channel-level headers;
        /// per-call entries win on key collision.
        /// </summary>
        public IDictionary<string, string> AdditionalHeaders { get; set; }
            = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // ── Channel-structural settings — not overrideable per call ──────────
        // These are always taken from the channel-level defaults during the merge;
        // any value supplied in a per-call override is silently ignored.

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
        /// The HTTP header that carries the Unix timestamp of the delivery.
        /// Defaults to <c>X-Webhook-Timestamp</c>.
        /// </summary>
        public string TimestampHeaderName { get; set; } = WebhookDefaults.TimestampHeaderName;

        /// <summary>
        /// The name of the HTTP header that communicates which signing algorithm was used.
        /// Set to <c>null</c> or empty to suppress this header.
        /// Defaults to <c>X-Webhook-Signature-Algorithm</c>.
        /// </summary>
        public string? SignatureAlgorithmHeaderName { get; set; }
            = WebhookDefaults.SignatureAlgorithmHeaderName;

        /// <summary>
        /// HTTP status codes considered transient and eligible for a retry.
        /// Defaults to 429, 500, 502, 503 and 504.
        /// </summary>
        public ISet<int> RetryableStatusCodes { get; set; }
            = new HashSet<int> { 429, 500, 502, 503, 504 };

        /// <summary>
        /// The name of the named <see cref="System.Net.Http.HttpClient"/> to resolve
        /// from <see cref="System.Net.Http.IHttpClientFactory"/>. When <c>null</c>
        /// the default channel name (<see cref="WebhookDefaults.HttpClientName"/>) is used.
        /// </summary>
        public string? HttpClientName { get; set; }
    }
}
