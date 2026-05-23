//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr {
    /// <summary>
    /// Raised when publishing through Azure Service Bus fails.
    /// </summary>
    public class ServiceBusPublishException : EventPublishException {
        /// <summary>
        /// Initializes the exception with an optional message and inner error.
        /// </summary>
        /// <param name="message">A message describing the Service Bus publish failure.</param>
        /// <param name="innerException">The underlying transport or serialization error.</param>
        public ServiceBusPublishException(string? message, Exception? innerException)
            : base(message, innerException) {
        }
    }
}



