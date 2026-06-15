//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Reflection;

namespace Hermodr
{
    /// <summary>
    /// Registry for event schemas with version-aware lookup capabilities.
    /// </summary>
    public interface IEventSchemaRegistry
    {
        /// <summary>
        /// Registers an event schema instance.
        /// </summary>
        /// <param name="schema">The schema to register.</param>
        void Register(IEventSchema schema);

        /// <summary>
        /// Registers an event schema by constructing it from a CLR type annotated with <see cref="EventAttribute"/>.
        /// </summary>
        /// <typeparam name="TData">The event data type.</typeparam>
        void Register<TData>() where TData : class;

        /// <summary>
        /// Registers an event schema by constructing it from a CLR type annotated with <see cref="EventAttribute"/>.
        /// </summary>
        /// <param name="dataType">The event data type.</param>
        void Register(Type dataType);

        /// <summary>
        /// Scans an assembly for types annotated with <see cref="EventAttribute"/> and registers their schemas.
        /// </summary>
        /// <param name="assembly">The assembly to scan.</param>
        void ScanAssembly(Assembly assembly);

        /// <summary>
        /// Gets the schema for the specified event type and version.
        /// </summary>
        /// <param name="eventType">The event type to look up.</param>
        /// <param name="version">The specific version to retrieve, or null to get the latest version.</param>
        /// <returns>The schema if found; otherwise, null.</returns>
        IEventSchema? GetSchema(string eventType, string? version = null);

        /// <summary>
        /// Gets all registered versions of the specified event type.
        /// </summary>
        /// <param name="eventType">The event type to retrieve versions for.</param>
        /// <returns>A list of all registered schemas for the event type, ordered by version.</returns>
        IReadOnlyList<IEventSchema> GetAllVersions(string eventType);
    }
}
