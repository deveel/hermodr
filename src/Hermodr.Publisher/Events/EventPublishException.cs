//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr {
    /// <summary>
    /// An exception that is thrown when an event cannot be published.
    /// </summary>
    public class EventPublishException : Exception {
        /// <summary>
        /// Constructs the empty exception.
        /// </summary>
        public EventPublishException() {
		}

        /// <summary>
        /// Constructs the exception with a message that describes 
        /// the error.
        /// </summary>
        /// <param name="message">
        /// The message that describes the error.
        /// </param>
		public EventPublishException(string? message) : base(message) {
		}

        /// <summary>
        /// Constructs the exception with a message that describes
        /// the error and an inner exception that is the cause of
        /// this exception.
        /// </summary>
        /// <param name="message">
        /// The message that describes the error.
        /// </param>
        /// <param name="innerException">
        /// The exception that is the cause of this exception.
        /// </param>
		public EventPublishException(string? message, Exception? innerException) : base(message, innerException) {
		}
	}
}
