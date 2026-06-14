//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Collections.Concurrent;
using System.Reflection;

namespace Hermodr
{
    /// <summary>
    /// Thread-safe registry for event schemas with version-aware lookup.
    /// </summary>
    public sealed class EventSchemaRegistry : IEventSchemaRegistry
    {
        private readonly ConcurrentDictionary<string, IEventSchema> _schemas = new(StringComparer.Ordinal);
        private readonly IEventSchemaFactory _factory;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventSchemaRegistry"/> class.
        /// </summary>
        /// <param name="factory">The schema factory used to construct schemas from CLR types.</param>
        public EventSchemaRegistry(IEventSchemaFactory factory)
        {
            _factory = factory;
        }

        /// <inheritdoc/>
        public void Register(IEventSchema schema)
        {
            ArgumentNullException.ThrowIfNull(schema);
            var key = BuildSchemaKey(schema.EventType, schema.Version.ToString());
            _schemas[key] = schema;
        }

        /// <inheritdoc/>
        public void Register<TData>() where TData : class
            => Register(_factory.CreateFromType<TData>());

        /// <inheritdoc/>
        public void Register(Type dataType)
        {
            ArgumentNullException.ThrowIfNull(dataType);
            var schema = _factory.CreateFromType(dataType);
            Register(schema);
        }

        /// <inheritdoc/>
        public void ScanAssembly(Assembly assembly)
        {
            ArgumentNullException.ThrowIfNull(assembly);

            var eventTypes = assembly.GetTypes()
                .Where(t => t.GetCustomAttribute<EventAttribute>() != null);

            foreach (var type in eventTypes)
            {
                var attr = type.GetCustomAttribute<EventAttribute>();
                // Skip types that use DataSchema URI instead of DataVersion
                if (attr != null && attr.DataVersion == null)
                    continue;

                try
                {
                    Register(type);
                }
                catch (Exception)
                {
                    // Skip types that cannot be registered (e.g., missing version)
                }
            }
        }

        /// <inheritdoc/>
        public IEventSchema? GetSchema(string eventType, string? version = null)
        {
            if (string.IsNullOrEmpty(eventType))
                return null;

            if (version != null)
            {
                // Exact version match
                var key = BuildSchemaKey(eventType, version);
                return _schemas.TryGetValue(key, out var schema) ? schema : null;
            }

            // No version specified - return latest version
            return _schemas
                .Where(kvp => kvp.Key.StartsWith(eventType + ":", StringComparison.Ordinal))
                .OrderByDescending(kvp => kvp.Value.Version)
                .Select(kvp => kvp.Value)
                .FirstOrDefault();
        }

        /// <inheritdoc/>
        public IReadOnlyList<IEventSchema> GetAllVersions(string eventType)
        {
            if (string.IsNullOrEmpty(eventType))
                return Array.Empty<IEventSchema>();

            return _schemas
                .Where(kvp => kvp.Key.StartsWith(eventType + ":", StringComparison.Ordinal))
                .OrderByDescending(kvp => kvp.Value.Version)
                .Select(kvp => kvp.Value)
                .ToList();
        }

        private static string BuildSchemaKey(string eventType, string version)
            => $"{eventType}:{version}";
    }
}
