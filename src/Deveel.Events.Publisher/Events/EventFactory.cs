//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

using Microsoft.Extensions.Options;

using System.Reflection;
using System.Text.Json;

namespace Deveel.Events
{
    /// <summary>
    /// Default <see cref="IEventFactory"/> implementation.
    /// Uses reflection to read <see cref="EventAttribute"/> and
    /// <see cref="EventAttributesAttribute"/> annotations from the data class and
    /// builds a <see cref="CloudNative.CloudEvents.CloudEvent"/> whose
    /// <c>data</c> field is the JSON-serialised instance.
    /// </summary>
    /// <remarks>
    /// Registered as a singleton by <see cref="EventPublisherBuilder"/> via
    /// <c>IEventFactory</c>. Replace it with a custom implementation using
    /// <see cref="EventPublisherBuilder.UsePublisher{TPublisher}"/> or by
    /// directly overriding <see cref="EventPublisher.CreateEventFromData"/>.
    /// </remarks>
    class EventFactory : IEventFactory
    {
        public EventFactory(IOptions<EventPublisherOptions>? publisherOptions = null)
        {
            PublisherOptions = publisherOptions?.Value ?? new EventPublisherOptions();
        }

        private EventPublisherOptions PublisherOptions { get; }

        public CloudEvent CreateEventFromData(Type dataType, object? data)
        {
            ArgumentNullException.ThrowIfNull(dataType, nameof(dataType));

            var eventAttribute = dataType.GetCustomAttribute<EventAttribute>();
            if (eventAttribute == null)
                throw new ArgumentException($"The type {dataType.FullName} is not an event type");

            var eventType = eventAttribute.EventType;
            var dataSchema = eventAttribute.DataSchema;
            var dataVersion = eventAttribute.DataVersion;
            var contentType = eventAttribute.ContentType;
            
            if (String.IsNullOrWhiteSpace(contentType))
                contentType = PublisherOptions.DefaultContentType;
            
            if (String.IsNullOrWhiteSpace(contentType))
                throw new InvalidOperationException("The content type for the event is not set");

            Uri? schemaUri = null;
            if (dataSchema == null && String.IsNullOrWhiteSpace(dataVersion))
                throw new ArgumentException($"The event type {eventType} does not have a schema or version");

            if (dataSchema != null)
            {
                schemaUri = dataSchema;
            } else if (!String.IsNullOrWhiteSpace(dataVersion))
            {
                var dataSchemaBaseUri = ResolveDataSchemaBaseUri(dataType);
                if (dataSchemaBaseUri == null)
                    throw new InvalidOperationException(
                        $"The event type '{eventType}' uses [Event] DataVersion '{dataVersion}', but neither EventPublisherOptions.DataSchemaBaseUri nor [assembly: EventDataSchemaUri(...)] is configured. " +
                        "Configure it via services.AddEventPublisher(options => options.DataSchemaBaseUri = new Uri(\"https://schemas.example.com/events\")).");

                var schemaUriBuilder = new UriBuilder(dataSchemaBaseUri);
                schemaUriBuilder.Path = $"{schemaUriBuilder.Path}/{eventType}/{dataVersion}";
                schemaUri = schemaUriBuilder.Uri;
            }

            var @event = new CloudEvent
            {
                Type = eventType,
                DataSchema = schemaUri,
                DataContentType = contentType,
                Data = JsonSerializer.Serialize(data, PublisherOptions.JsonSerializerOptions)
            };

            var eventAttrs = dataType.GetCustomAttributes<EventAttributesAttribute>();
            foreach (var attr in eventAttrs)
            {
                @event[attr.AttributeName] = attr.Value;
            }

            return @event;
        }

        private Uri? ResolveDataSchemaBaseUri(Type dataType)
        {
            if (PublisherOptions.DataSchemaBaseUri != null)
                return PublisherOptions.DataSchemaBaseUri;

            var assemblyAttribute = dataType.Assembly.GetCustomAttribute<EventDataSchemaUriAttribute>();
            if (assemblyAttribute == null)
                return null;

            return new Uri(assemblyAttribute.BaseUri, UriKind.Absolute);
        }
    }
}
