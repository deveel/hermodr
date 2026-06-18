//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr
{
    /// <summary>
    /// Thrown when an event cannot be upcast to the requested schema version.
    /// </summary>
    public class EventUpcastingException : Exception
    {
        /// <summary>
        /// Creates a new <see cref="EventUpcastingException"/>.
        /// </summary>
        /// <param name="message">The error message.</param>
        public EventUpcastingException(string message) : base(message)
        {
        }

        /// <summary>
        /// Creates a new <see cref="EventUpcastingException"/>.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="innerException">The exception that caused this error.</param>
        public EventUpcastingException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
