//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hermodr
{
    /// <summary>
    /// Extension methods for <see cref="EventPublisherBuilder"/> that add schema validation
    /// to the event publishing pipeline.
    /// </summary>
    public static class EventPublisherBuilderExtensions
    {
        /// <summary>
        /// Adds schema validation middleware to the publisher pipeline.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This middleware validates every event's payload against its registered schema
        /// before the event reaches any publish channel. Validation is performed using
        /// format-agnostic deserialization (JSON, XML, etc.) and checks constraints such
        /// as required fields, range limits, and enum values.
        /// </para>
        /// <para>
        /// If schema services (<see cref="IEventSchemaRegistry"/>, <see cref="IEventSchemaValidator"/>,
        /// etc.) have not been registered yet, they are automatically registered by this method.
        /// </para>
        /// </remarks>
        /// <param name="builder">The publisher builder to configure.</param>
        /// <param name="configureOptions">
        /// An optional action to customize <see cref="SchemaValidationOptions"/>.
        /// </param>
        /// <param name="configureSchemaServices">
        /// An optional action to configure schema service options (assembly scanning, explicit registrations).
        /// Only used when schema services are not already registered.
        /// </param>
        /// <returns>The same <paramref name="builder"/> for chaining.</returns>
        /// <example>
        /// <code language="csharp">
        /// services.AddEventPublisher()
        ///     .UseSchemaValidation(options =>
        ///     {
        ///         options.MissingSchemaBehavior = MissingSchemaBehavior.Block;
        ///         options.ValidationFailureBehavior = ValidationFailureBehavior.Throw;
        ///     }, schemaOptions =>
        ///     {
        ///         schemaOptions.AssembliesToScan.Add(typeof(Program).Assembly);
        ///     })
        ///     .AddChannel&lt;RabbitMqPublishChannel&gt;();
        /// </code>
        /// </example>
        public static EventPublisherBuilder UseSchemaValidation(
            this EventPublisherBuilder builder,
            Action<SchemaValidationOptions>? configureOptions = null,
            Action<EventSchemaServicesOptions>? configureSchemaServices = null)
        {
            ArgumentNullException.ThrowIfNull(builder);

            // Auto-register schema services if not already registered
            var schemaRegistryRegistered = builder.Services.Any(
                d => d.ServiceType == typeof(IEventSchemaRegistry));

            if (!schemaRegistryRegistered)
            {
                builder.Services.AddEventSchemaServices(configureSchemaServices);
            }
            else if (configureSchemaServices != null)
            {
                builder.Services.Configure(configureSchemaServices);
            }

            // Configure validation options
            if (configureOptions != null)
            {
                builder.Services.Configure(configureOptions);
            }

            // Pre-register middleware in DI so all constructor dependencies are resolved
            builder.Services.TryAddSingleton<SchemaValidationMiddleware>();

            // Add middleware to pipeline
            builder.Use<SchemaValidationMiddleware>();

            return builder;
        }
    }
}
