//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Deveel.Events
{
    /// <summary>
    /// Raised when webhook delivery fails because of network/transport errors.
    /// </summary>
    public class WebhookTransportException : WebhookDeliveryException
    {
        /// <summary>
        /// Initializes the exception with a message and the underlying transport error.
        /// </summary>
        /// <param name="message">A message describing the delivery failure.</param>
        /// <param name="innerException">The underlying transport exception.</param>
        public WebhookTransportException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}



