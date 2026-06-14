//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hermodr
{
    /// <summary>
    /// Extension methods for registering event schema services with dependency injection.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds event schema services including the schema factory, validator, registry, and deserializers.
        /// </summary>
        /// <param name="services">The service collection to add services to.</param>
        /// <param name="configureOptions">An optional action to configure schema service options.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddEventSchemaServices(
            this IServiceCollection services,
            Action<EventSchemaServicesOptions>? configureOptions = null)
        {
            bool initializerRegistered = services.Any(d => d.ServiceType == typeof(SchemaRegistrationsApplier));

            if (!initializerRegistered)
            {
                // Configure options (always do this, even if already registered)
                if (configureOptions != null)
                {
                    services.Configure(configureOptions);
                }

                // Core services
                services.TryAddSingleton<IEventSchemaFactory, EventSchemaFactory>();
                services.TryAddSingleton<IEventSchemaValidator, EventSchemaValidator>();
                services.TryAddSingleton<IEventSchemaRegistry, EventSchemaRegistry>();
                services.TryAddSingleton<IEventDataDeserializerRegistry, EventDataDeserializerRegistry>();

                // Built-in deserializers
                services.TryAddSingleton<IEventDataDeserializer, JsonEventDataDeserializer>();
                services.TryAddSingleton<IEventDataDeserializer, XmlEventDataDeserializer>();

                // Register the applier as a singleton - its constructor runs once at resolution
                services.TryAddSingleton<SchemaRegistrationsApplier>();
            }
            else if (configureOptions != null)
            {
                services.Configure(configureOptions);
            }

            return services;
        }
    }

    /// <summary>
    /// Applies schema registrations (assembly scanning and explicit registrations)
    /// when first resolved from the DI container.
    /// </summary>
    public sealed class SchemaRegistrationsApplier
    {
        /// <summary>
        /// Initializes the schema registrations applier and applies all pending registrations.
        /// </summary>
        public SchemaRegistrationsApplier(
            Microsoft.Extensions.Options.IOptions<EventSchemaServicesOptions> options,
            IEventSchemaRegistry registry,
            IEventDataDeserializerRegistry deserializerRegistry,
            IEnumerable<IEventDataDeserializer> deserializers)
        {
            var opts = options.Value;

            // Register built-in deserializers (JSON, XML, etc.) first
            foreach (var deserializer in deserializers)
                deserializerRegistry.Register(deserializer);

            // Then additional deserializers from options
            foreach (var deserializer in opts.AdditionalDeserializers)
                deserializerRegistry.Register(deserializer);

            foreach (var assembly in opts.AssembliesToScan)
                registry.ScanAssembly(assembly);

            foreach (var registration in opts.SchemaRegistrations)
                registration(registry);
        }
    }
}