//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Deveel.Events
{
    /// <summary>
    /// Exception thrown when a webhook delivery fails after all retry attempts.
    /// </summary>
    public class WebhookDeliveryException : Exception
    {
        /// <summary>
        /// Initialises the exception with a message and the last HTTP status code received.
        /// </summary>
        public WebhookDeliveryException(string message, int statusCode)
            : base(message)
        {
            StatusCode = statusCode;
        }

        /// <summary>
        /// Initialises the exception with a message and the inner exception that caused the failure.
        /// </summary>
        public WebhookDeliveryException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// The last HTTP status code returned by the endpoint, or <c>0</c> if the
        /// failure was caused by a network-level error.
        /// </summary>
        public int StatusCode { get; }
    }
}
