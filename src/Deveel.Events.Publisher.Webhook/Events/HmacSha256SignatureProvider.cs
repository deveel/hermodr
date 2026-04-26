//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Security.Cryptography;

namespace Deveel.Events
{
    /// <summary>
    /// <see cref="IWebhookSignatureProvider"/> that computes an HMAC-SHA-256
    /// signature. Produces a value of the form <c>sha256=&lt;hex&gt;</c>.
    /// </summary>
    /// <remarks>
    /// This is the most widely supported algorithm across webhook providers
    /// (GitHub, Stripe, Slack, Twilio, Shopify, and many others) and is the default
    /// for <see cref="WebhookPublishChannel"/>.
    /// </remarks>
    public class HmacSha256SignatureProvider : HmacWebhookSignatureProvider
    {
        /// <summary>A shared singleton instance.</summary>
        public static readonly HmacSha256SignatureProvider Default = new();

        /// <inheritdoc/>
        public override WebhookSignatureAlgorithm Algorithm => WebhookSignatureAlgorithm.HmacSha256;

        /// <inheritdoc/>
        public override string AlgorithmName => "hmac-sha256";

        /// <inheritdoc/>
        protected override string SignaturePrefix => "sha256";

        /// <inheritdoc/>
        protected override byte[] ComputeHash(byte[] message, byte[] key)
        {
            using var hmac = new HMACSHA256(key);
            return hmac.ComputeHash(message);
        }
    }
}
