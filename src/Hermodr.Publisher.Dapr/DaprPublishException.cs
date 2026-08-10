//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr {
    /// <summary>
    /// Raised when publishing through the Dapr pub/sub building block fails.
    /// </summary>
    public class DaprPublishException : EventPublishException {
        /// <summary>
        /// Initializes the exception with an optional message and inner error.
        /// </summary>
        /// <param name="message">A message describing the Dapr failure.</param>
        /// <param name="innerException">The underlying transport error.</param>
        public DaprPublishException(string? message, Exception? innerException)
            : base(message, innerException) {
        }
    }
}