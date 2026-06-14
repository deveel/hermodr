//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr
{
    /// <summary>
    /// Defines the behavior when no schema is registered for an event type.
    /// </summary>
    public enum MissingSchemaBehavior
    {
        /// <summary>
        /// Skip validation when no schema is found for the event type.
        /// The event is published without schema validation.
        /// </summary>
        Allow = 0,

        /// <summary>
        /// Throw a <see cref="SchemaValidationException"/> when no schema is found
        /// for the event type.
        /// </summary>
        Block = 1,

        /// <summary>
        /// Use a default "object" schema (no constraints) when no schema is found.
        /// The event is published without constraint validation.
        /// </summary>
        Fallback = 2
    }
}
