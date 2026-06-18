//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr
{
    /// <summary>
    /// Configuration options for the event upcasting middleware.
    /// </summary>
    public class EventUpcastingOptions
    {
        /// <summary>
        /// Gets or sets whether the upcasting middleware is active.
        /// Defaults to <c>true</c>.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the behavior when no upcaster chain can be built for a legacy event.
        /// Defaults to <see cref="MissingUpcasterBehavior.PassThrough"/>.
        /// </summary>
        public MissingUpcasterBehavior MissingUpcasterBehavior { get; set; } = MissingUpcasterBehavior.PassThrough;

        /// <summary>
        /// Gets or sets whether the middleware should attempt to update the event's
        /// <c>dataschema</c> URI when the schema version changes.
        /// Defaults to <c>true</c>.
        /// </summary>
        public bool UpdateDataSchema { get; set; } = true;
    }
}
