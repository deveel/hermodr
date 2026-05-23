//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr
{
    /// <summary>
    /// The HMAC algorithm used to compute the webhook request signature.
    /// </summary>
    public enum WebhookSignatureAlgorithm
    {
        /// <summary>
        /// HMAC-SHA-256. The most widely adopted algorithm across webhook providers
        /// (GitHub, Stripe, Slack, Twilio, etc.). Recommended default.
        /// </summary>
        HmacSha256,

        /// <summary>
        /// HMAC-SHA-384. Part of the SHA-2 family; a good middle-ground between
        /// SHA-256 and SHA-512 when a larger digest is required.
        /// </summary>
        HmacSha384,

        /// <summary>
        /// HMAC-SHA-512. Provides the largest digest in the SHA-2 family;
        /// used by several fintech and high-security APIs.
        /// </summary>
        HmacSha512,

        /// <summary>
        /// HMAC-SHA-1. Legacy algorithm retained for compatibility with older
        /// webhook consumers. <b>Not recommended</b> for new integrations —
        /// SHA-1 is considered cryptographically weak.
        /// </summary>
        HmacSha1,
    }
}
