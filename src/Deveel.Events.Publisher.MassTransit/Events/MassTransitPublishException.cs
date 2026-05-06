//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Deveel.Events {
    /// <summary>
    /// Raised when publishing through the MassTransit transport fails.
    /// </summary>
    public class MassTransitPublishException : EventPublishException {
        /// <summary>
        /// Initializes the exception with an optional message and inner error.
        /// </summary>
        /// <param name="message">A message describing the MassTransit failure.</param>
        /// <param name="innerException">The underlying transport error.</param>
        public MassTransitPublishException(string? message, Exception? innerException)
            : base(message, innerException) {
        }
    }
}


