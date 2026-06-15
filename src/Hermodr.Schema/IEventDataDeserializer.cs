//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr
{
    /// <summary>
    /// Deserializes event data from a specific content type into a format-agnostic object tree.
    /// </summary>
    public interface IEventDataDeserializer
    {
        /// <summary>
        /// Gets the content type this deserializer handles.
        /// </summary>
        string ContentType { get; }

        /// <summary>
        /// Determines if this deserializer can handle the specified content type.
        /// </summary>
        /// <param name="contentType">The content type to check.</param>
        /// <returns>True if this deserializer can handle the content type; otherwise, false.</returns>
        bool CanDeserialize(string contentType);

        /// <summary>
        /// Deserializes the event data stream into a format-agnostic object tree.
        /// </summary>
        /// <param name="data">The data stream to deserialize.</param>
        /// <param name="cancellationToken">A token to cancel the deserialization operation.</param>
        /// <returns>A deserialized object tree (typically Dictionary&lt;string, object?&gt;), or null if the data is empty.</returns>
        Task<object?> DeserializeAsync(Stream data, CancellationToken cancellationToken);
    }
}
