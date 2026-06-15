//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.ComponentModel.DataAnnotations;

namespace Hermodr
{
    /// <summary>
    /// Exception thrown when schema validation fails during event publishing.
    /// </summary>
    public class SchemaValidationException : Exception
    {
        /// <summary>
        /// Gets the list of validation errors that caused the exception.
        /// </summary>
        public IReadOnlyList<ValidationResult> Errors { get; }

        /// <summary>
        /// Gets the event type that was being validated, if known.
        /// </summary>
        public string? EventType { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SchemaValidationException"/> class.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="errors">The validation errors.</param>
        /// <param name="eventType">The event type being validated.</param>
        public SchemaValidationException(
            string message,
            IReadOnlyList<ValidationResult>? errors = null,
            string? eventType = null)
            : base(message)
        {
            Errors = errors ?? Array.Empty<ValidationResult>();
            EventType = eventType;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SchemaValidationException"/> class
        /// with a single validation error.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="error">The single validation error.</param>
        /// <param name="eventType">The event type being validated.</param>
        public SchemaValidationException(
            string message,
            ValidationResult error,
            string? eventType = null)
            : base(message)
        {
            Errors = error != null
                ? new[] { error }
                : Array.Empty<ValidationResult>();
            EventType = eventType;
        }
    }
}
