//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Deveel.Events {
    /// <summary>
    /// An exception that is thrown when a <see cref="CloudNative.CloudEvents.CloudEvent"/>
    /// is missing one or more required CloudEvents attributes.
    /// </summary>
    /// <remarks>
    /// The four required attributes defined by the CloudEvents specification are
    /// <c>id</c>, <c>source</c>, <c>type</c>, and <c>specversion</c>.
    /// This exception is raised by <see cref="EventPublisher"/> after attribute
    /// enrichment and before the event is dispatched to any publish channel,
    /// so that brokers never receive a structurally invalid envelope.
    /// </remarks>
    public class InvalidCloudEventException : ArgumentException {
        /// <summary>
        /// Constructs the exception with the names of the attributes that are
        /// missing from the event.
        /// </summary>
        /// <param name="missingAttributes">
        /// The names of the CloudEvents attributes that are absent or empty.
        /// </param>
        public InvalidCloudEventException(IReadOnlyList<string> missingAttributes)
            : base(BuildMessage(missingAttributes)) {
            MissingAttributes = missingAttributes;
        }

        /// <summary>
        /// Constructs the exception with a pre-formatted message and the names
        /// of the missing attributes.
        /// </summary>
        /// <param name="message">
        /// A human-readable description of the error.
        /// </param>
        /// <param name="missingAttributes">
        /// The names of the CloudEvents attributes that are absent or empty.
        /// </param>
        public InvalidCloudEventException(string? message, IReadOnlyList<string> missingAttributes)
            : base(message) {
            MissingAttributes = missingAttributes;
        }

        /// <summary>
        /// Gets the names of the required CloudEvents attributes that were
        /// absent or empty at publish time.
        /// </summary>
        public IReadOnlyList<string> MissingAttributes { get; }

        private static string BuildMessage(IReadOnlyList<string> missing) {
            if (missing == null || missing.Count == 0)
                return "The CloudEvent is invalid: one or more required attributes are missing.";

            var attrs = string.Join(", ", missing);
            return $"The CloudEvent is missing the following required attribute(s): {attrs}.";
        }
    }
}

