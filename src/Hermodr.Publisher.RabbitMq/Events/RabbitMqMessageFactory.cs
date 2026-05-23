//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;
using CloudNative.CloudEvents.SystemTextJson;

using Microsoft.Extensions.Options;

using System.Text.Json;

namespace Hermodr
{
    /// <summary>
    /// A default implementation of the <see cref="IRabbitMqMessageFactory"/>
    /// that creates a message to be published to a RabbitMQ queue.
    /// </summary>
    public sealed class RabbitMqMessageFactory : IRabbitMqMessageFactory
    {
        private readonly RabbitMqPublishOptions _options;

        /// <summary>
        /// Constructs the factory with the options to use for creating messages.
        /// </summary>
        /// <param name="options">
        /// The RabbitMQ channel options to use for creating messages.
        /// </param>
        public RabbitMqMessageFactory(IOptions<RabbitMqPublishOptions> options)
        {
            _options = options.Value;
        }

        /// <inheritdoc />
        public RabbitMqMessage CreateMessage(CloudEvent @event)
        {
            var messageContent = _options.MessageContent ?? RabbitMqMessageContent.CloudEvent;
            var messageFormat  = _options.MessageFormat  ?? RabbitMqMessageFormat.Json;

            switch (messageContent)
            {
                case RabbitMqMessageContent.CloudEvent:
                    {
                        var body = messageFormat switch
                        {
                            RabbitMqMessageFormat.Json   => GetCloudEventAsJsonBytes(@event),
                            RabbitMqMessageFormat.Binary => GetCloudEventAsBinary(@event),
                            _ => throw new InvalidOperationException("The message format is not supported")
                        };

                        var contentType = messageFormat switch
                        {
                            RabbitMqMessageFormat.Json   => "application/cloudevents+json",
                            RabbitMqMessageFormat.Binary => "application/cloudevents+octet-stream",
                            _ => throw new InvalidOperationException("The message format is not supported")
                        };

                        return new RabbitMqMessage(body, contentType, contentType == "application/cloudevents+json" ? "utf-8" : null);
                    }

                case RabbitMqMessageContent.EventData:
                    {
                        var body = messageFormat switch
                        {
                            RabbitMqMessageFormat.Json   => GetEventDataAsJsonBytes(@event),
                            RabbitMqMessageFormat.Binary => GetEventDataAsBinary(@event),
                            _ => throw new NotSupportedException($"The message format {messageFormat} is not supported")
                        };

                        var contentType = messageFormat switch
                        {
                            RabbitMqMessageFormat.Json   => "application/json",
                            RabbitMqMessageFormat.Binary => "application/octet-stream",
                            _ => throw new NotSupportedException($"The message format {messageFormat} is not supported")
                        };

                        return new RabbitMqMessage(body, contentType, contentType == "application/json" ? "utf-8" : null);
                    }
            }

            throw new NotSupportedException($"The message content {_options.MessageContent} is not supported");
        }

        private ReadOnlyMemory<byte> GetEventDataAsJsonBytes(CloudEvent @event)
        {
            return JsonSerializer.SerializeToUtf8Bytes(@event.Data, _options.JsonSerializerOptions ?? JsonSerializerOptions.Default);
        }

        private ReadOnlyMemory<byte> GetEventDataAsBinary(CloudEvent @event)
        {
            var formatter = new JsonEventFormatter(_options.JsonSerializerOptions ?? JsonSerializerOptions.Default, new JsonDocumentOptions());
            return formatter.EncodeBinaryModeEventData(@event);
        }

        private ReadOnlyMemory<byte> GetCloudEventAsBinary(CloudEvent @event)
        {
            throw new NotSupportedException();
        }

        private ReadOnlyMemory<byte> GetCloudEventAsJsonBytes(CloudEvent @event)
        {
            var formatter = new JsonEventFormatter(_options.JsonSerializerOptions ?? JsonSerializerOptions.Default, new JsonDocumentOptions());
            var json = formatter.ConvertToJsonElement(@event);
            return JsonSerializer.SerializeToUtf8Bytes(json, _options.JsonSerializerOptions);

        }
    }
}
