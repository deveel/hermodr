//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Text.Json.Nodes;

namespace Hermodr
{
    /// <summary>
    /// Transforms event data from an older schema version to a newer one.
    /// </summary>
    public interface IEventUpcaster
    {
        /// <summary>
        /// Returns <c>true</c> when this upcaster can transform the specified event type
        /// from <paramref name="fromVersion"/> to <paramref name="toVersion"/>.
        /// </summary>
        /// <param name="eventType">The event type.</param>
        /// <param name="fromVersion">The source schema version.</param>
        /// <param name="toVersion">The target schema version.</param>
        bool CanUpcast(string eventType, Version fromVersion, Version toVersion);

        /// <summary>
        /// Applies the transformation described by <paramref name="context"/>.
        /// </summary>
        /// <param name="context">The upcast context.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The transformed data.</returns>
        Task<JsonNode> UpcastAsync(UpcastContext context, CancellationToken cancellationToken = default);
    }
}
