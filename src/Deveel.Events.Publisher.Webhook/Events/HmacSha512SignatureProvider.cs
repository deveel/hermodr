//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Security.Cryptography;

namespace Deveel.Events
{
    /// <summary>
    /// <see cref="IWebhookSignatureProvider"/> that computes an HMAC-SHA-512
    /// signature. Produces a value of the form <c>sha512=&lt;hex&gt;</c>.
    /// </summary>
    /// <remarks>
    /// SHA-512 provides the largest digest in the SHA-2 family and is used by
    /// several fintech and high-security API providers as an optional stronger
    /// alternative to SHA-256.
    /// </remarks>
    public sealed class HmacSha512SignatureProvider : HmacWebhookSignatureProvider
    {
        /// <summary>A shared singleton instance.</summary>
        public static readonly HmacSha512SignatureProvider Default = new();

        /// <inheritdoc/>
        public override WebhookSignatureAlgorithm Algorithm => WebhookSignatureAlgorithm.HmacSha512;

        /// <inheritdoc/>
        public override string AlgorithmName => "hmac-sha512";

        /// <inheritdoc/>
        protected override string SignaturePrefix => "sha512";

        /// <inheritdoc/>
        protected override byte[] ComputeHash(byte[] message, byte[] key)
        {
            using var hmac = new HMACSHA512(key);
            return hmac.ComputeHash(message);
        }
    }
}
