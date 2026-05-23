//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

using System.ComponentModel.DataAnnotations;

namespace Hermodr {
    /// <summary>
    /// A service that is used to validate an event against a schema
    /// that describes its structure.
    /// </summary>
    public interface IEventSchemaValidator {
        /// <summary>
        /// Validates the given event against the schema provided.
        /// </summary>
        /// <param name="schema">
        /// The schema that describes the structure of the event.
        /// </param>
        /// <param name="event">
        /// The instance of the event to validate.
        /// </param>
        /// <param name="cancellationToken">
        /// A token that can be used to cancel the validation.
        /// </param>
        /// <returns>
        /// Returns an asynchronous stream of validation results.
        /// </returns>
		IAsyncEnumerable<ValidationResult> ValidateEventAsync(IEventSchema schema, CloudEvent @event, CancellationToken cancellationToken = default);
	}
}
