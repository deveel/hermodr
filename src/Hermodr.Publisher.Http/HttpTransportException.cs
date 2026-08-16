//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr
{
    /// <summary>
    /// Raised when an HTTP publish to an endpoint fails because of network/transport
    /// errors (connection failures, timeouts, DNS errors, ...) after all retries.
    /// </summary>
    public class HttpTransportException : HttpPublishException
    {
        /// <summary>
        /// Initializes the exception with a message.
        /// </summary>
        /// <param name="message">A message describing the delivery failure.</param>
        public HttpTransportException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes the exception with a message and the underlying transport error.
        /// </summary>
        /// <param name="message">A message describing the delivery failure.</param>
        /// <param name="innerException">The underlying transport exception.</param>
        public HttpTransportException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}