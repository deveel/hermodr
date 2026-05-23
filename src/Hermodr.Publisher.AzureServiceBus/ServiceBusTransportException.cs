//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr {
    /// <summary>
    /// Raised when Azure Service Bus rejects or fails the transport operation.
    /// </summary>
    public class ServiceBusTransportException : ServiceBusPublishException {
        /// <summary>
        /// Initializes the exception with an optional message and inner error.
        /// </summary>
        /// <param name="message">A message describing the transport failure.</param>
        /// <param name="innerException">The underlying Service Bus exception.</param>
        public ServiceBusTransportException(string? message, Exception? innerException)
            : base(message, innerException) {
        }
    }
}

