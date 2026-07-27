//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Reflection;

namespace Hermodr
{
    /// <summary>
    /// Discovers <see cref="IEventSchema"/> instances from CLR assemblies by
    /// scanning for types annotated with <see cref="EventAttribute"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the export-time counterpart of <see cref="EventSchemaRegistry.ScanAssembly"/>
    /// and is intended for AsyncAPI / OpenAPI document generation. It does not
    /// register schemas with <see cref="IEventSchemaRegistry"/>; callers receive
    /// a list of freshly built schemas that can be passed to a document builder.
    /// </para>
    /// <para>
    /// Types whose <see cref="EventAttribute"/> carries an absolute
    /// <see cref="EventAttribute.DataSchema"/> URI (rather than a
    /// <see cref="EventAttribute.DataVersion"/> string) are skipped, mirroring
    /// the behaviour of <see cref="EventSchemaRegistry.ScanAssembly"/>.
    /// </para>
    /// <para>
    /// The discovery process uses reflection and is therefore not suitable for
    /// trimmed / AOT-compiled deployments. The compile-time source generator
    /// planned for v1.5.0 (roadmap item 21) will provide an AOT-safe alternative.
    /// </para>
    /// </remarks>
    public sealed class EventSchemaDiscovery
    {
        private readonly IEventSchemaFactory _factory;

        /// <summary>
        /// Creates a new discovery service using the default
        /// <see cref="EventSchemaFactory"/>.
        /// </summary>
        public EventSchemaDiscovery()
            : this(EventSchemaFactory.Default)
        {
        }

        /// <summary>
        /// Creates a new discovery service using the given <paramref name="factory"/>.
        /// </summary>
        /// <param name="factory">
        /// The factory used to build <see cref="EventSchema"/> instances from
        /// annotated CLR types.
        /// </param>
        public EventSchemaDiscovery(IEventSchemaFactory factory)
        {
            ArgumentNullException.ThrowIfNull(factory);
            _factory = factory;
        }

        /// <summary>
        /// Scans the given assemblies for types annotated with
        /// <see cref="EventAttribute"/> and returns a schema for each discoverable
        /// type.
        /// </summary>
        /// <param name="assemblies">The assemblies to scan.</param>
        /// <returns>
        /// A list of <see cref="IEventSchema"/> instances, one per discoverable
        /// event type. Types that fail to produce a schema (missing version,
        /// unconstructible, ...) are silently skipped; the discovery never throws
        /// for a single bad type.
        /// </returns>
        public IReadOnlyList<IEventSchema> DiscoverSchemas(params Assembly[] assemblies)
        {
            ArgumentNullException.ThrowIfNull(assemblies);

            var schemas = new List<IEventSchema>();

            foreach (var assembly in assemblies)
            {
                ArgumentNullException.ThrowIfNull(assembly);

                IEnumerable<Type> eventTypes;
                try
                {
                    eventTypes = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException)
                {
                    // Some assemblies (native, dynamic) cannot be fully loaded;
                    // skip them gracefully.
                    continue;
                }

                foreach (var type in eventTypes)
                {
                    var attr = type.GetCustomAttribute<EventAttribute>();
                    if (attr == null) continue;
                    // Skip types that use a DataSchema URI instead of a DataVersion,
                    // mirroring EventSchemaRegistry.ScanAssembly.
                    if (attr.DataVersion == null) continue;

                    try
                    {
                        var schema = _factory.CreateFromType(type);
                        schemas.Add(schema);
                    }
                    catch (InvalidOperationException)
                    {
                        // Skip types that cannot be turned into a schema.
                    }
                    catch (ArgumentException)
                    {
                        // Skip types that cannot be turned into a schema.
                    }
                    catch (NotSupportedException)
                    {
                        // Skip types that cannot be turned into a schema.
                    }
                }
            }

            return schemas;
        }
    }
}