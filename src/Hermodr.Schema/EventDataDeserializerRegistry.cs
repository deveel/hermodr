//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Collections.Concurrent;

namespace Hermodr
{
    /// <summary>
    /// Thread-safe registry for event data deserializers with content-type matching.
    /// </summary>
    public sealed class EventDataDeserializerRegistry : IEventDataDeserializerRegistry
    {
        private readonly ConcurrentDictionary<string, IEventDataDeserializer> _deserializers = new(StringComparer.OrdinalIgnoreCase);

        /// <inheritdoc/>
        public void Register(IEventDataDeserializer deserializer)
        {
            ArgumentNullException.ThrowIfNull(deserializer);
            _deserializers[deserializer.ContentType.ToLowerInvariant()] = deserializer;
        }

        /// <inheritdoc/>
        public IEventDataDeserializer? GetDeserializer(string contentType)
        {
            if (string.IsNullOrEmpty(contentType))
                return null;

            // Try exact match first (case-insensitive)
            var normalizedContentType = contentType.ToLowerInvariant();
            if (_deserializers.TryGetValue(normalizedContentType, out var deserializer))
                return deserializer;

            // Try wildcard prefix match (e.g., "application/*" → "application/json")
            var prefix = contentType.Split('/')[0].ToLowerInvariant();
            foreach (var kvp in _deserializers)
            {
                if (kvp.Key.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase))
                    return kvp.Value;
            }

            return null;
        }
    }
}
