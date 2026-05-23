//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr {
    /// <summary>
    /// Raised when a convertible payload fails to produce a CloudEvent.
    /// </summary>
    public class EventConversionException : EventPublishException {
        /// <summary>
        /// Initializes the exception with an optional message and inner error.
        /// </summary>
        /// <param name="message">A message describing the conversion failure.</param>
        /// <param name="innerException">The underlying conversion error.</param>
        public EventConversionException(string? message, Exception? innerException)
            : base(message, innerException) {
        }
    }
}

