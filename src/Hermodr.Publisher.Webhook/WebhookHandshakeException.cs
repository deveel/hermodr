//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr
{
    /// <summary>
    /// Thrown when the
    /// <see href="https://github.com/cloudevents/spec/blob/main/cloudevents/http-webhook.md">CloudEvents HTTP WebHooks</see>
    /// <c>OPTIONS</c> validation handshake fails and
    /// <see cref="WebhookDiscoveryOptions.RequireSuccessfulHandshake"/> is set.
    /// </summary>
    /// <remarks>
    /// A handshake is considered failed when the target explicitly refuses
    /// permission (does not return <c>WebHook-Allowed-Origin</c>), responds
    /// with a non-success status code, or the request itself faults.
    /// When <see cref="WebhookDiscoveryOptions.RequireSuccessfulHandshake"/>
    /// is <c>false</c> the channel logs the failure and proceeds with the
    /// delivery instead of throwing.
    /// </remarks>
    public class WebhookHandshakeException : Exception
    {
        /// <summary>
        /// Creates a new handshake exception.
        /// </summary>
        /// <param name="message">The reason the handshake failed.</param>
        /// <param name="endpointUrl">The endpoint that was being validated.</param>
        /// <param name="statusCode">
        /// The HTTP status code returned by the target, when applicable.
        /// </param>
        /// <param name="innerException">
        /// The underlying exception, when the handshake request itself faulted.
        /// </param>
        public WebhookHandshakeException(
            string message,
            string endpointUrl,
            int? statusCode = null,
            Exception? innerException = null)
            : base(message, innerException)
        {
            EndpointUrl = endpointUrl;
            StatusCode = statusCode;
        }

        /// <summary>The endpoint that was being validated.</summary>
        public string EndpointUrl { get; }

        /// <summary>
        /// The HTTP status code returned by the target, when applicable.
        /// </summary>
        public int? StatusCode { get; }
    }
}