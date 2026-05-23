//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr {
    /// <summary>
    /// Raised when a channel fails while dispatching an event.
    /// </summary>
    public class EventPublishChannelException : EventPublishException {
        /// <summary>
        /// Initializes the exception with an optional message and inner error.
        /// </summary>
        /// <param name="message">A message describing the channel failure.</param>
        /// <param name="innerException">The underlying channel error.</param>
        public EventPublishChannelException(string? message, Exception? innerException)
            : base(message, innerException) {
        }
    }
}



