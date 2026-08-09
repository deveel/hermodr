//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr
{
    /// <summary>
    /// The result of a
    /// <see href="https://github.com/cloudevents/spec/blob/main/cloudevents/http-webhook.md">CloudEvents HTTP WebHooks</see>
    /// <c>OPTIONS</c> validation handshake — i.e. the permission granted by
    /// the delivery target via the <c>WebHook-Allowed-Origin</c> and
    /// <c>WebHook-Allowed-Rate</c> response headers.
    /// </summary>
    /// <remarks>
    /// Instances are produced by the webhook channel's handshake client and
    /// cached per endpoint so that subsequent deliveries reuse the granted
    /// permission. The host can inspect the granted
    /// <see cref="AllowedRate"/> to apply its own throttling policy; the
    /// channel itself does not auto-throttle.
    /// </remarks>
    public sealed class WebhookDiscoveryPermission
    {
        /// <summary>
        /// Creates a new permission record from a handshake response.
        /// </summary>
        /// <param name="allowedOrigin">
        /// The value of the <c>WebHook-Allowed-Origin</c> header returned by
        /// the target, or <c>"*"</c> indicating permission from all origins.
        /// </param>
        /// <param name="allowedRate">
        /// The value of the <c>WebHook-Allowed-Rate</c> header returned by the
        /// target, expressed as requests per minute, or <c>null</c> when the
        /// target did not return the header (or returned <c>*</c>).
        /// </param>
        public WebhookDiscoveryPermission(string allowedOrigin, int? allowedRate)
        {
            AllowedOrigin = allowedOrigin ?? throw new ArgumentNullException(nameof(allowedOrigin));
            AllowedRate = allowedRate;
        }

        /// <summary>
        /// The origin the delivery target has agreed to receive notifications
        /// from. May be <c>"*"</c> to indicate permission from all origins.
        /// </summary>
        public string AllowedOrigin { get; }

        /// <summary>
        /// The maximum request rate (requests per minute) granted by the
        /// target, or <c>null</c> when the target did not constrain the rate.
        /// </summary>
        public int? AllowedRate { get; }

        /// <summary>
        /// The UTC time at which the handshake response was received.
        /// </summary>
        public DateTimeOffset GrantedAt { get; } = DateTimeOffset.UtcNow;
    }
}