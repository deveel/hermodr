//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Security.Cryptography;

namespace Deveel.Events
{
    /// <summary>
    /// <see cref="IWebhookSignatureProvider"/> that computes an HMAC-SHA-1
    /// signature. Produces a value of the form <c>sha1=&lt;hex&gt;</c>.
    /// </summary>
    /// <remarks>
    /// <b>SHA-1 is cryptographically weak.</b> This implementation is provided
    /// solely for backward compatibility with legacy webhook consumers that have
    /// not yet migrated to a stronger algorithm. Do not use it for new integrations.
    /// </remarks>
    [Obsolete(
        "SHA-1 is considered cryptographically weak. " +
        "Use HmacSha256SignatureProvider or HmacSha512SignatureProvider for new integrations.",
        error: false)]
    public sealed class HmacSha1SignatureProvider : HmacWebhookSignatureProvider
    {
        /// <summary>A shared singleton instance.</summary>
        public static readonly HmacSha1SignatureProvider Default = new();

        /// <inheritdoc/>
        public override WebhookSignatureAlgorithm Algorithm => WebhookSignatureAlgorithm.HmacSha1;

        /// <inheritdoc/>
        public override string AlgorithmName => "hmac-sha1";

        /// <inheritdoc/>
        protected override string SignaturePrefix => "sha1";

        /// <inheritdoc/>
        protected override byte[] ComputeHash(byte[] message, byte[] key)
        {
            using var hmac = new HMACSHA1(key);
            return hmac.ComputeHash(message);
        }
    }
}
