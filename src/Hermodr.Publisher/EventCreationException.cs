//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr {
    /// <summary>
    /// Raised when a CloudEvent cannot be created from source data.
    /// </summary>
    public class EventCreationException : EventPublishException {
        /// <summary>
        /// Initializes the exception with an optional message and inner error.
        /// </summary>
        /// <param name="message">A message describing the event creation failure.</param>
        /// <param name="innerException">The underlying creation error.</param>
        public EventCreationException(string? message, Exception? innerException)
            : base(message, innerException) {
        }
    }
}

