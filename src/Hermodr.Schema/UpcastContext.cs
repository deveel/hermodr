//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Text.Json.Nodes;

namespace Hermodr
{
    /// <summary>
    /// Carries the state for a single upcasting transformation.
    /// </summary>
    public sealed class UpcastContext
    {
        /// <summary>
        /// Creates a new <see cref="UpcastContext"/>.
        /// </summary>
        /// <param name="eventType">The event type being upcast.</param>
        /// <param name="fromVersion">The source schema version.</param>
        /// <param name="toVersion">The target schema version.</param>
        /// <param name="data">The event data as a <see cref="JsonNode"/>.</param>
        public UpcastContext(string eventType, Version fromVersion, Version toVersion, JsonNode data)
        {
            EventType = eventType ?? throw new ArgumentNullException(nameof(eventType));
            FromVersion = fromVersion ?? throw new ArgumentNullException(nameof(fromVersion));
            ToVersion = toVersion ?? throw new ArgumentNullException(nameof(toVersion));
            Data = data ?? throw new ArgumentNullException(nameof(data));
        }

        /// <summary>The event type being upcast.</summary>
        public string EventType { get; }

        /// <summary>The source schema version.</summary>
        public Version FromVersion { get; }

        /// <summary>The target schema version.</summary>
        public Version ToVersion { get; }

        /// <summary>
        /// The event data as a <see cref="JsonNode"/>. Implementations may mutate this value
        /// or replace it with a new node.
        /// </summary>
        public JsonNode Data { get; set; }
    }
}
