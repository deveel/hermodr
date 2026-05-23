//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr
{
    /// <summary>
    /// Default constant values used by the webhook publisher.
    /// </summary>
    public static class WebhookDefaults
    {
        /// <summary>Default name for the HMAC-SHA256 signature header.</summary>
        public const string SignatureHeaderName = "X-Webhook-Signature";

        /// <summary>Default name for the unique delivery identifier header.</summary>
        public const string DeliveryIdHeaderName = "X-Webhook-Delivery";

        /// <summary>Default name for the event type header.</summary>
        public const string EventTypeHeaderName = "X-Webhook-Event";

        /// <summary>Default name for the Unix timestamp header (seconds since epoch).</summary>
        public const string TimestampHeaderName = "X-Webhook-Timestamp";

        /// <summary>
        /// Default name for the header that communicates which signing algorithm
        /// was used, allowing the receiver to validate the payload with the same
        /// algorithm. Example value: <c>hmac-sha256</c>.
        /// </summary>
        public const string SignatureAlgorithmHeaderName = "X-Webhook-Signature-Algorithm";

        /// <summary>Named HttpClient registered for the webhook publisher.</summary>
        public const string HttpClientName = "Hermodr.Webhook";
    }
}

