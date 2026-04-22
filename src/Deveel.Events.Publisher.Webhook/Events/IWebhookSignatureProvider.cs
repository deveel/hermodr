//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Deveel.Events
{
    /// <summary>
    /// Computes the cryptographic signature for a webhook delivery.
    /// </summary>
    /// <remarks>
    /// Implementations are keyed by <see cref="Algorithm"/> so that the channel
    /// can resolve the correct provider at runtime based on the configured
    /// <see cref="WebhookEventPublishChannelOptions.SignatureAlgorithm"/>.
    /// </remarks>
    public interface IWebhookSignatureProvider
    {
        /// <summary>
        /// The <see cref="WebhookSignatureAlgorithm"/> this provider handles.
        /// </summary>
        WebhookSignatureAlgorithm Algorithm { get; }

        /// <summary>
        /// The algorithm identifier sent in the
        /// <c>X-Webhook-Signature-Algorithm</c> header so that the receiver
        /// knows which algorithm to use when validating the payload.
        /// Examples: <c>hmac-sha256</c>, <c>hmac-sha512</c>.
        /// </summary>
        string AlgorithmName { get; }

        /// <summary>
        /// Computes the signature for the given payload and timestamp.
        /// </summary>
        /// <param name="payload">The raw request body bytes.</param>
        /// <param name="timestamp">
        /// Unix timestamp (seconds since epoch) prepended to the signed
        /// message to protect against replay attacks.
        /// </param>
        /// <param name="secret">The shared secret key.</param>
        /// <returns>
        /// A string of the form <c>{prefix}={hex-digest}</c>,
        /// e.g. <c>sha256=abc123…</c>.
        /// </returns>
        string ComputeSignature(byte[] payload, long timestamp, string secret);
    }
}
