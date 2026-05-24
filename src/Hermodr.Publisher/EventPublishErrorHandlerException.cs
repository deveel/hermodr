//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr {
    /// <summary>
    /// Raised when an error handler fails while processing a publish error.
    /// </summary>
    public class EventPublishErrorHandlerException : EventPublishException {
        /// <summary>
        /// Initializes the exception with an optional message and inner error.
        /// </summary>
        /// <param name="message">A message describing the error-handler failure.</param>
        /// <param name="innerException">The underlying error.</param>
        public EventPublishErrorHandlerException(string? message, Exception? innerException)
            : base(message, innerException) {
        }
    }
}

