//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hermodr
{
    /// <summary>
    /// Middleware that validates event payloads against their registered schemas
    /// before the event reaches any publish channel.
    /// </summary>
    public sealed class SchemaValidationMiddleware : IEventMiddleware
    {
        private readonly IEventSchemaRegistry _schemaRegistry;
        private readonly IEventSchemaValidator _validator;
        private readonly SchemaValidationOptions _options;
        private readonly ILogger<SchemaValidationMiddleware> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="SchemaValidationMiddleware"/> class.
        /// </summary>
        /// <param name="schemaRegistry">The schema registry for schema lookup.</param>
        /// <param name="validator">The schema validator.</param>
        /// <param name="options">The validation options.</param>
        /// <param name="logger">A logger instance.</param>
        /// <param name="initializer">Ensures schema registrations are applied before first validation.</param>
        public SchemaValidationMiddleware(
            IEventSchemaRegistry schemaRegistry,
            IEventSchemaValidator validator,
            IOptions<SchemaValidationOptions> options,
            ILogger<SchemaValidationMiddleware> logger,
            SchemaRegistrationsApplier registrations)
        {
            _schemaRegistry = schemaRegistry;
            _validator = validator;
            _options = options?.Value ?? new SchemaValidationOptions();
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task InvokeAsync(EventContext context, EventPublishDelegate next)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(next);

            var eventType = context.Event.Type;
            var version = context.Event["dataversion"] as string;

            // Lookup schema by event type + version
            var schema = string.IsNullOrEmpty(eventType) ? null : _schemaRegistry.GetSchema(eventType, version);

            if (schema == null)
            {
                switch (_options.MissingSchemaBehavior)
                {
                    case MissingSchemaBehavior.Block:
                        throw new SchemaValidationException(
                            $"No schema registered for event type '{eventType}'" +
                            (version != null ? $" version '{version}'" : ""),
                            eventType: eventType);

                    case MissingSchemaBehavior.Fallback:
                        // Use a default empty schema (no constraints)
                        schema = CreateFallbackSchema(eventType, version);
                        break;

                    case MissingSchemaBehavior.Allow:
                    default:
                        _logger.LogDebug(
                            "No schema found for event type {EventType}{Version}, skipping validation",
                            eventType,
                            version != null ? $" (version {version})" : "");
                        await next(context);
                        return;
                }
            }

            // Validate event payload against schema
            var errors = new List<ValidationResult>();
            await foreach (var error in _validator.ValidateEventAsync(schema, context.Event, context.CancellationToken))
            {
                errors.Add(error);
            }

            if (errors.Count > 0)
            {
                var exception = new SchemaValidationException(
                    $"Schema validation failed for event type '{eventType}'" +
                    (version != null ? $" version '{version}'" : "") +
                    $" with {errors.Count} error(s): {string.Join("; ", errors.Select(e => e.ErrorMessage))}",
                    errors,
                    eventType);

                if (_options.LogValidationErrors)
                {
                    _logger.LogError(exception,
                        "Schema validation failed for event type {EventType}{Version} with {ErrorCount} error(s)",
                        eventType,
                        version != null ? $" (version {version})" : "",
                        errors.Count);
                }

                if (_options.ValidationFailureBehavior == ValidationFailureBehavior.Throw)
                {
                    throw exception;
                }
            }

            await next(context);
        }

        private static IEventSchema CreateFallbackSchema(string eventType, string? version)
        {
            return new EventSchema(
                eventType,
                version ?? "1.0",
                "object");
        }
    }
}
