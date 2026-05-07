//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Deveel.Events
{
    /// <summary>
    /// Raised when webhook delivery fails with a non-success HTTP status code.
    /// </summary>
    public class WebhookStatusCodeException : WebhookDeliveryException
    {
        /// <summary>
        /// Initializes the exception with a message and the last HTTP status code.
        /// </summary>
        /// <param name="message">A message describing the delivery failure.</param>
        /// <param name="statusCode">The HTTP status code returned by the endpoint.</param>
        public WebhookStatusCodeException(string message, int statusCode)
            : base(message, statusCode)
        {
        }
    }
}

