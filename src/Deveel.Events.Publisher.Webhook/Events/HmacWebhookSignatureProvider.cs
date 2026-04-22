//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Text;

namespace Deveel.Events
{
    /// <summary>
    /// Base class for HMAC-based <see cref="IWebhookSignatureProvider"/>
    /// implementations.
    /// </summary>
    /// <remarks>
    /// The signed message is <c>{timestamp}.{body}</c> (dot-separated), which
    /// mirrors the convention used by Stripe, GitHub and most major webhook
    /// providers. Receivers should verify the timestamp to mitigate replay attacks.
    /// </remarks>
    public abstract class HmacWebhookSignatureProvider : IWebhookSignatureProvider
    {
        /// <inheritdoc/>
        public abstract WebhookSignatureAlgorithm Algorithm { get; }

        /// <inheritdoc/>
        public abstract string AlgorithmName { get; }

        /// <summary>
        /// The short prefix embedded in the signature value, e.g. <c>sha256</c>.
        /// </summary>
        protected abstract string SignaturePrefix { get; }

        /// <inheritdoc/>
        public string ComputeSignature(byte[] payload, long timestamp, string secret)
        {
            ArgumentNullException.ThrowIfNull(payload, nameof(payload));
            ArgumentNullException.ThrowIfNull(secret, nameof(secret));

            // Signed message = "{timestamp}.{body}"
            var prefix  = Encoding.UTF8.GetBytes($"{timestamp}.");
            var message = new byte[prefix.Length + payload.Length];
            Buffer.BlockCopy(prefix,  0, message, 0,             prefix.Length);
            Buffer.BlockCopy(payload, 0, message, prefix.Length, payload.Length);

            var keyBytes = Encoding.UTF8.GetBytes(secret);
            var hash     = ComputeHash(message, keyBytes);

            return $"{SignaturePrefix}={Convert.ToHexString(hash).ToLowerInvariant()}";
        }

        /// <summary>
        /// Computes the HMAC hash of <paramref name="message"/> using
        /// <paramref name="key"/>.
        /// </summary>
        protected abstract byte[] ComputeHash(byte[] message, byte[] key);
    }
}
