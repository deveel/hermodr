//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr
{
    /// <summary>
    /// Registry for event data deserializers, enabling content-type-based deserializer resolution.
    /// </summary>
    public interface IEventDataDeserializerRegistry
    {
        /// <summary>
        /// Registers a deserializer for a specific content type.
        /// </summary>
        /// <param name="deserializer">The deserializer to register.</param>
        void Register(IEventDataDeserializer deserializer);

        /// <summary>
        /// Gets the deserializer that can handle the specified content type.
        /// </summary>
        /// <param name="contentType">The content type to find a deserializer for.</param>
        /// <returns>The deserializer if found; otherwise, null.</returns>
        IEventDataDeserializer? GetDeserializer(string contentType);
    }
}
