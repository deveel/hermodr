//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr {
    /// <summary>
    /// Raised when a CloudEvent cannot be serialized into a Service Bus message.
    /// </summary>
    public class ServiceBusSerializationException : ServiceBusPublishException {
        /// <summary>
        /// Initializes the exception with an optional message and inner error.
        /// </summary>
        /// <param name="message">A message describing the serialization failure.</param>
        /// <param name="innerException">The underlying serialization error.</param>
        public ServiceBusSerializationException(string? message, Exception? innerException)
            : base(message, innerException) {
        }
    }
}

