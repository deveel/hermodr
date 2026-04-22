//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Security.Cryptography;

namespace Deveel.Events
{
    /// <summary>
    /// <see cref="IWebhookSignatureProvider"/> that computes an HMAC-SHA-384
    /// signature. Produces a value of the form <c>sha384=&lt;hex&gt;</c>.
    /// </summary>
    public sealed class HmacSha384SignatureProvider : HmacWebhookSignatureProvider
    {
        /// <summary>A shared singleton instance.</summary>
        public static readonly HmacSha384SignatureProvider Default = new();

        /// <inheritdoc/>
        public override WebhookSignatureAlgorithm Algorithm => WebhookSignatureAlgorithm.HmacSha384;

        /// <inheritdoc/>
        public override string AlgorithmName => "hmac-sha384";

        /// <inheritdoc/>
        protected override string SignaturePrefix => "sha384";

        /// <inheritdoc/>
        protected override byte[] ComputeHash(byte[] message, byte[] key)
        {
            using var hmac = new HMACSHA384(key);
            return hmac.ComputeHash(message);
        }
    }
}
