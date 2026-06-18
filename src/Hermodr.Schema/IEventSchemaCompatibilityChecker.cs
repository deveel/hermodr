//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr
{
    /// <summary>
    /// Compares two versions of an event schema and reports their compatibility.
    /// </summary>
    public interface IEventSchemaCompatibilityChecker
    {
        /// <summary>
        /// Compares an older schema with a newer schema and returns a detailed compatibility result.
        /// </summary>
        /// <param name="older">The older schema version.</param>
        /// <param name="newer">The newer schema version.</param>
        /// <returns>A <see cref="SchemaCompatibilityResult"/> describing the detected changes.</returns>
        SchemaCompatibilityResult Check(IEventSchema older, IEventSchema newer);
    }
}
