//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Text.Json.Nodes;

using CloudNative.CloudEvents;

namespace Hermodr
{
    /// <summary>
    /// Adapts a specific event data serialization format to the <see cref="JsonNode"/> tree
    /// used by the upcasting pipeline.
    /// </summary>
    /// <remarks>
    /// Implementations allow the upcasting pipeline to support formats other than JSON.
    /// The built-in <see cref="JsonEventUpcastingDataAdapter"/> handles JSON and CloudEvents JSON.
    /// </remarks>
    public interface IEventUpcastingDataAdapter
    {
        /// <summary>
        /// Returns <c>true</c> when this adapter can convert the specified content type.
        /// </summary>
        /// <param name="contentType">The CloudEvent <c>datacontenttype</c> value.</param>
        bool CanUpcast(string contentType);

        /// <summary>
        /// Reads the data payload of <paramref name="event"/> into a <see cref="JsonNode"/>.
        /// </summary>
        /// <param name="event">The event whose data should be read.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The data as a <see cref="JsonNode"/>.</returns>
        Task<JsonNode> ReadAsync(CloudEvent @event, CancellationToken cancellationToken = default);

        /// <summary>
        /// Writes the transformed <paramref name="data"/> back into <paramref name="event"/>.
        /// </summary>
        /// <param name="event">The event to update.</param>
        /// <param name="data">The upcasted data.</param>
        /// <param name="contentType">The target content type.</param>
        void Write(CloudEvent @event, JsonNode data, string contentType);
    }
}
