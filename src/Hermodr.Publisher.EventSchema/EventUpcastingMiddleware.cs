//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Text.Json.Nodes;

using CloudNative.CloudEvents;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Hermodr
{
    /// <summary>
    /// Middleware that upcasts legacy event payloads to the latest registered schema version
    /// before the event reaches downstream middleware and channels.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The middleware inspects the <c>dataversion</c> extension (falling back to the last
    /// segment of the <c>dataschema</c> URI) and compares it with the latest version registered
    /// in <see cref="IEventSchemaRegistry"/> for the event type. When the event is behind,
    /// it runs the registered upcasting pipeline and rewrites the event data, version, and schema.
    /// </para>
    /// <para>
    /// For the best results, register this middleware <em>before</em> schema validation so that
    /// upcasted events validate against the latest schema.
    /// </para>
    /// </remarks>
    public sealed class EventUpcastingMiddleware : IEventMiddleware
    {
        private readonly EventUpcastingOptions _options;
        private readonly ILogger<EventUpcastingMiddleware> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly Lazy<Task> _initializationTask;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventUpcastingMiddleware"/> class.
        /// </summary>
        /// <param name="options">The upcasting options.</param>
        /// <param name="logger">A logger instance.</param>
        /// <param name="serviceProvider">The service provider for resolving version-aware services.</param>
        public EventUpcastingMiddleware(
            IOptions<EventUpcastingOptions> options,
            ILogger<EventUpcastingMiddleware> logger,
            IServiceProvider serviceProvider)
        {
            _options = options?.Value ?? new EventUpcastingOptions();
            _logger = logger ?? NullLogger<EventUpcastingMiddleware>.Instance;
            _serviceProvider = serviceProvider;
            _initializationTask = new Lazy<Task>(InitializeAsync, LazyThreadSafetyMode.ExecutionAndPublication);
        }

        private async Task InitializeAsync()
        {
            var options = _serviceProvider.GetRequiredService<IOptions<EventSchemaServicesOptions>>();
            var registry = _serviceProvider.GetRequiredService<IEventSchemaRegistry>();
            var deserializerRegistry = _serviceProvider.GetRequiredService<IEventDataDeserializerRegistry>();
            var deserializers = _serviceProvider.GetServices<IEventDataDeserializer>();

            foreach (var deserializer in deserializers)
                deserializerRegistry.Register(deserializer);

            foreach (var deserializer in options.Value.AdditionalDeserializers)
                deserializerRegistry.Register(deserializer);

            foreach (var assembly in options.Value.AssembliesToScan)
                registry.ScanAssembly(assembly);

            foreach (var registration in options.Value.SchemaRegistrations)
                registration(registry);
        }

        /// <inheritdoc/>
        public async Task InvokeAsync(EventContext context, EventPublishDelegate next)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(next);

            await _initializationTask.Value;

            if (!_options.Enabled)
            {
                await next(context);
                return;
            }

            var eventType = context.Event.Type;
            if (string.IsNullOrEmpty(eventType))
            {
                await next(context);
                return;
            }

            var currentVersion = GetCurrentVersion(context.Event);
            if (currentVersion == null)
            {
                _logger.LogDebug(
                    "Event {EventType} has no dataversion or dataschema version; skipping upcasting",
                    eventType);
                await next(context);
                return;
            }

            var schemaRegistry = _serviceProvider.GetRequiredService<IEventSchemaRegistry>();
            var latestSchema = schemaRegistry.GetSchema(eventType);
            if (latestSchema == null)
            {
                _logger.LogDebug("No schema registered for {EventType}; skipping upcasting", eventType);
                await next(context);
                return;
            }

            if (!Version.TryParse(latestSchema.Version, out var targetVersion))
            {
                _logger.LogWarning(
                    "Latest schema version '{Version}' for {EventType} is not a valid version",
                    latestSchema.Version,
                    eventType);
                await next(context);
                return;
            }

            if (currentVersion >= targetVersion)
            {
                await next(context);
                return;
            }

            var contentType = context.Event.DataContentType ?? "application/json";
            var adapter = _serviceProvider.GetServices<IEventUpcastingDataAdapter>()
                .FirstOrDefault(a => a.CanUpcast(contentType));

            if (adapter == null)
            {
                _logger.LogDebug(
                    "No upcasting adapter for content type {ContentType}; skipping upcasting",
                    contentType);
                await next(context);
                return;
            }

            JsonNode data;
            try
            {
                data = await adapter.ReadAsync(context.Event, context.CancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read event data for upcasting; skipping");
                await next(context);
                return;
            }

            var pipeline = _serviceProvider.GetRequiredService<IEventUpcastingPipeline>();
            try
            {
                data = await pipeline.UpcastAsync(
                    eventType,
                    data,
                    currentVersion,
                    targetVersion,
                    context.CancellationToken);
            }
            catch (EventUpcastingException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to upcast event {EventType} from {FromVersion} to {ToVersion}",
                    eventType,
                    currentVersion,
                    targetVersion);

                if (_options.MissingUpcasterBehavior == MissingUpcasterBehavior.Throw)
                    throw;

                await next(context);
                return;
            }

            adapter.Write(context.Event, data, contentType);

            var dataVersionAttr = CloudEventAttribute.CreateExtension("dataversion", CloudEventAttributeType.String);
            context.Event[dataVersionAttr] = targetVersion.ToString();

            if (_options.UpdateDataSchema)
            {
                var updatedSchema = UpdateDataSchemaVersion(context.Event.DataSchema, currentVersion, targetVersion);
                if (updatedSchema != null)
                    context.Event.DataSchema = updatedSchema;
            }

            await next(context);
        }

        private static Version? GetCurrentVersion(CloudEvent @event)
        {
            if (@event["dataversion"] is string dataVersion &&
                Version.TryParse(dataVersion, out var version))
            {
                return version;
            }

            if (@event.DataSchema != null)
            {
                var lastSegment = @event.DataSchema.Segments.LastOrDefault()?.TrimEnd('/');
                if (!string.IsNullOrEmpty(lastSegment) &&
                    Version.TryParse(lastSegment, out var schemaVersion))
                {
                    return schemaVersion;
                }
            }

            return null;
        }

        private static Uri? UpdateDataSchemaVersion(Uri? dataSchema, Version currentVersion, Version targetVersion)
        {
            if (dataSchema == null)
                return null;

            var path = dataSchema.AbsolutePath;
            var currentSegment = currentVersion.ToString();

            if (!path.EndsWith("/" + currentSegment) && !path.EndsWith(currentSegment))
                return null;

            var newPath = path.Substring(0, path.Length - currentSegment.Length) + targetVersion;
            var builder = new UriBuilder(dataSchema) { Path = newPath };
            return builder.Uri;
        }
    }
}
