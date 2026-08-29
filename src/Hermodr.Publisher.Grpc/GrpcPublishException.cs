//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Grpc.Core;

namespace Hermodr
{
    /// <summary>
    /// Exception thrown when a gRPC publish to one or more endpoints fails,
    /// or when an endpoint returns a non-OK gRPC status.
    /// </summary>
    /// <remarks>
    /// Because a single publish call fans out to every configured endpoint, the
    /// exception can represent failures from several endpoints: the
    /// <see cref="Failures"/> collection lists each individual endpoint failure.
    /// The <see cref="EventPublisher"/> error-handling pipeline (including the
    /// dead-letter channel) engages automatically when this exception is thrown.
    /// </remarks>
    public class GrpcPublishException : Exception
    {
        /// <summary>
        /// Initializes the exception with a message.
        /// </summary>
        /// <param name="message">The message that describes the publish failure.</param>
        public GrpcPublishException(string message)
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
        public GrpcPublishException(string message, Exception innerException)
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
        public GrpcPublishException(string message, IEnumerable<Exception> failures)
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
        /// Gets the gRPC status code of the last failure, or
        /// <see cref="StatusCode.OK"/> when the failure was caused by a
        /// network-level error with no gRPC status.
        /// </summary>
        public StatusCode StatusCode { get; protected set; } = StatusCode.OK;
    }
}
