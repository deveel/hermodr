//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Reflection;

namespace Hermodr
{
    /// <summary>
    /// Configuration options for event schema services.
    /// </summary>
    public class EventSchemaServicesOptions
    {
        /// <summary>
        /// Gets the list of assemblies to scan for types annotated with <see cref="EventAttribute"/>.
        /// Scanning occurs at application startup after all services are registered.
        /// </summary>
        public IList<Assembly> AssembliesToScan { get; } = new List<Assembly>();

        /// <summary>
        /// Gets the list of explicit schema registration actions to execute at startup.
        /// </summary>
        public IList<Action<IEventSchemaRegistry>> SchemaRegistrations { get; } = new List<Action<IEventSchemaRegistry>>();

        /// <summary>
        /// Gets the list of additional deserializers to register beyond the built-in JSON and XML deserializers.
        /// </summary>
        public IList<IEventDataDeserializer> AdditionalDeserializers { get; } = new List<IEventDataDeserializer>();
    }
}
