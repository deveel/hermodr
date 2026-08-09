//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr
{
    /// <summary>
    /// Configures the
    /// <see href="https://github.com/cloudevents/spec/blob/main/cloudevents/http-webhook.md">CloudEvents HTTP WebHooks</see>
    /// abuse-protection handshake performed by the webhook publisher.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When attached to <see cref="WebhookPublishOptions.Discovery"/>, the channel
    /// lazily issues an <c>OPTIONS</c> pre-flight validation request to the
    /// endpoint before the first delivery, sending
    /// <see cref="RequestOrigin"/> and (when set)
    /// <see cref="RequestCallback"/> / <see cref="RequestRate"/>.
    /// The receiver grants permission by returning
    /// <c>WebHook-Allowed-Origin</c> and <c>WebHook-Allowed-Rate</c> headers;
    /// the result is cached per endpoint and reused for subsequent deliveries.
    /// </para>
    /// <para>
    /// After a successful handshake, <see cref="RequestOrigin"/> is attached
    /// to every delivery POST as the <c>WebHook-Request-Origin</c> header,
    /// as required by §2.1 of the WebHooks specification.
    /// </para>
    /// <para>
    /// The channel does <b>not</b> host the
    /// <c>WebHook-Request-Callback</c> endpoint: the callback URL is merely
    /// advertised to the receiver (which may invoke it asynchronously to
    /// grant permission), and is typically pointed at a separate receiver
    /// /subscription service.
    /// </para>
    /// </remarks>
    public class WebhookDiscoveryOptions
    {
        /// <summary>
        /// The DNS name that identifies the sending system, sent as the
        /// <c>WebHook-Request-Origin</c> header of the <c>OPTIONS</c>
        /// validation request and of every subsequent delivery POST.
        /// Required.
        /// </summary>
        public string RequestOrigin { get; set; } = string.Empty;

        /// <summary>
        /// An optional HTTPS URL advertised via the
        /// <c>WebHook-Request-Callback</c> header, allowing the delivery
        /// target to grant send permission asynchronously (e.g. via a browser
        /// or out-of-band request). The channel does not host this endpoint.
        /// </summary>
        public string? RequestCallback { get; set; }

        /// <summary>
        /// An optional desired request rate (requests per minute) sent via the
        /// <c>WebHook-Request-Rate</c> header. The receiver may grant a
        /// different rate in <c>WebHook-Allowed-Rate</c>.
        /// </summary>
        public int? RequestRate { get; set; }

        /// <summary>
        /// The timeout for the <c>OPTIONS</c> validation request.
        /// When <c>null</c> the channel uses the same timeout as delivery
        /// requests (<see cref="WebhookPublishOptions.RequestTimeout"/>,
        /// default 30 s).
        /// </summary>
        public TimeSpan? RequestTimeout { get; set; }

        /// <summary>
        /// When <c>true</c> the channel requires a successful validation
        /// handshake before any delivery is attempted; a refused or failed
        /// handshake causes <see cref="Hermodr.WebhookPublishChannel"/>
        /// to throw a <see cref="WebhookHandshakeException"/> and the delivery
        /// is not sent. When <c>false</c> (the default) a failed handshake
        /// is logged but the delivery proceeds (best-effort compliance).
        /// </summary>
        public bool RequireSuccessfulHandshake { get; set; }
    }
}