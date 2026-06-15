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

            return services;
        }
    }
}