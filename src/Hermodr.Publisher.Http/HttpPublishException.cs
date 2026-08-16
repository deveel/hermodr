//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr
{
    /// <summary>
    /// Exception thrown when an HTTP publish to one or more endpoints fails after
    /// all retry attempts, or when an endpoint returns a non-success status code.
    /// </summary>
    /// <remarks>
    /// Because a single publish call fans out to every configured endpoint, the
    /// exception can represent failures from several endpoints: the
    /// <see cref="Failures"/> collection lists each individual endpoint failure.
    /// The <see cref="EventPublisher"/> error-handling pipeline (including the
    /// dead-letter channel) engages automatically when this exception is thrown.
    /// </remarks>
    public class HttpPublishException : Exception
    {
        /// <summary>
        /// Initializes the exception with a message.
        /// </summary>
        /// <param name="message">The message that describes the publish failure.</param>
        public HttpPublishException(string message)
            : base(message)
        {
            Failures = Array.Empty<Exception>();
        }

        /// <summary>
        /// Initializes the exception with a message and an inner exception.
        /// </summary>
        /// <param name="message">The message that describes the publish failure.</param>
        /// <param name="innerException">
        /// The underlying exception that caused the failure.
        /// </param>
        public HttpPublishException(string message, Exception innerException)
            : base(message, innerException)
        {
            Failures = [innerException];
        }

        /// <summary>
        /// Initializes the exception with a message and the collection of individual
        /// endpoint failures.
        /// </summary>
        /// <param name="message">The message that describes the publish failure.</param>
        /// <param name="failures">
        /// The per-endpoint exceptions produced during the fan-out delivery.
        /// </param>
        public HttpPublishException(string message, IEnumerable<Exception> failures)
            : base(message)
        {
            Failures = (failures ?? []).ToList();
        }

        /// <summary>
        /// Gets the individual failures produced by the fan-out delivery to the
        /// configured endpoints. When a single endpoint failed, this collection
        /// contains exactly one element.
        /// </summary>
        public IReadOnlyList<Exception> Failures { get; }

        /// <summary>
        /// Gets the last HTTP status code that caused the failure, or <c>0</c> when
        /// the failure was caused by a network-level error.
        /// </summary>
        public int StatusCode { get; protected set; }
    }
}