//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Deveel.Events
{
    /// <summary>
    /// The original, single-algorithm HMAC-SHA-256 signature provider.
    /// </summary>
    /// <remarks>
    /// This class is preserved for backward compatibility.
    /// New code should use <see cref="HmacSha256SignatureProvider"/> directly.
    /// </remarks>
    [Obsolete("Use HmacSha256SignatureProvider instead.", error: false)]
    public sealed class WebhookSignatureProvider : HmacSha256SignatureProvider
    {
        /// <summary>A shared singleton instance.</summary>
        public static new readonly WebhookSignatureProvider Default = new();
    }
}
