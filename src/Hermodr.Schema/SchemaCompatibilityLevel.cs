//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr
{
    /// <summary>
    /// Describes the compatibility level between two versions of an event schema.
    /// </summary>
    public enum SchemaCompatibilityLevel
    {
        /// <summary>
        /// The schemas are not compatible in either direction.
        /// </summary>
        None,

        /// <summary>
        /// The newer schema can read events produced with the older schema.
        /// </summary>
        Backward,

        /// <summary>
        /// The older schema can read events produced with the newer schema.
        /// </summary>
        Forward,

        /// <summary>
        /// The schemas are compatible in both directions.
        /// </summary>
        Full
    }
}
