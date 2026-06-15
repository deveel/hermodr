//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr
{
    /// <summary>
    /// Configuration options for schema validation middleware.
    /// </summary>
    public class SchemaValidationOptions
    {
        /// <summary>
        /// Gets or sets the behavior when no schema is registered for an event type.
        /// Defaults to <see cref="MissingSchemaBehavior.Allow"/> for backward compatibility.
        /// </summary>
        public MissingSchemaBehavior MissingSchemaBehavior { get; set; } = MissingSchemaBehavior.Allow;

        /// <summary>
        /// Gets or sets the behavior when schema validation fails.
        /// Defaults to <see cref="ValidationFailureBehavior.Throw"/> for fail-fast validation.
        /// </summary>
        public ValidationFailureBehavior ValidationFailureBehavior { get; set; } = ValidationFailureBehavior.Throw;

        /// <summary>
        /// Gets or sets whether validation errors are logged even when not thrown.
        /// Defaults to <c>true</c>.
        /// </summary>
        public bool LogValidationErrors { get; set; } = true;

        /// <summary>
        /// Gets the list of content types that should be validated.
        /// When empty, all content types with a registered deserializer are validated.
        /// </summary>
        public IList<string> SupportedContentTypes { get; } = new List<string>();
    }
}
