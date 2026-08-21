//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr
{
    /// <summary>
    /// Raised when an HTTP publish to an endpoint fails with a non-success HTTP
    /// status code after all retries.
    /// </summary>
    public class HttpStatusCodeException : HttpPublishException
    {
        /// <summary>
        /// Initializes the exception with a message and the last HTTP status code.
        /// </summary>
        /// <param name="message">A message describing the delivery failure.</param>
        /// <param name="statusCode">The HTTP status code returned by the endpoint.</param>
        public HttpStatusCodeException(string message, int statusCode)
            : base(message)
        {
            StatusCode = statusCode;
        }
    }
}