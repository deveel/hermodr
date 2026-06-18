//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Text.Json.Nodes;

namespace Hermodr
{
    /// <summary>
    /// Chains registered <see cref="IEventUpcaster"/> instances to migrate event data
    /// from one schema version to another.
    /// </summary>
    public interface IEventUpcastingPipeline
    {
        /// <summary>
        /// Upcasts event data from <paramref name="fromVersion"/> to <paramref name="toVersion"/>.
        /// </summary>
        /// <param name="eventType">The event type.</param>
        /// <param name="data">The event data to upcast.</param>
        /// <param name="fromVersion">The current schema version.</param>
        /// <param name="toVersion">The target schema version.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The upcasted data.</returns>
        /// <exception cref="EventUpcastingException">Thrown when no upcaster chain can be built.</exception>
        Task<JsonNode> UpcastAsync(
            string eventType,
            JsonNode data,
            Version fromVersion,
            Version toVersion,
            CancellationToken cancellationToken = default);
    }
}
