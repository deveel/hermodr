//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Text.Json;
using System.Text.Json.Nodes;

using CloudNative.CloudEvents;

namespace Hermodr
{
    /// <summary>
    /// Adapts JSON and CloudEvents JSON payloads to the upcasting pipeline.
    /// </summary>
    public sealed class JsonEventUpcastingDataAdapter : IEventUpcastingDataAdapter
    {
        /// <inheritdoc/>
        public bool CanUpcast(string contentType)
        {
            if (string.IsNullOrEmpty(contentType))
                return false;

            return contentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase)
                || contentType.StartsWith("application/cloudevents", StringComparison.OrdinalIgnoreCase);
        }

        /// <inheritdoc/>
        public Task<JsonNode> ReadAsync(CloudEvent @event, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(@event);

            return @event.Data switch
            {
                JsonNode node => Task.FromResult(node),
                string json => Task.FromResult(ParseOrThrow(json)),
                byte[] bytes => Task.FromResult(ParseOrThrow(bytes)),
                Stream stream => ReadStreamAsync(stream, cancellationToken),
                _ => throw new EventUpcastingException(
                    $"Cannot read JSON data from CloudEvent: unsupported data type '{@event.Data?.GetType().Name}'.")
            };
        }

        /// <inheritdoc/>
        public void Write(CloudEvent @event, JsonNode data, string contentType)
        {
            ArgumentNullException.ThrowIfNull(@event);
            ArgumentNullException.ThrowIfNull(data);
            ArgumentException.ThrowIfNullOrEmpty(contentType);

            @event.Data = data.ToJsonString(GetJsonSerializerOptions(contentType));
            @event.DataContentType = contentType;
        }

        private static Task<JsonNode> ReadStreamAsync(Stream stream, CancellationToken cancellationToken)
        {
            if (stream is MemoryStream memoryStream)
            {
                var bytes = memoryStream.ToArray();
                return Task.FromResult(ParseOrThrow(bytes));
            }

            return ReadStreamCoreAsync(stream, cancellationToken);
        }

        private static async Task<JsonNode> ReadStreamCoreAsync(Stream stream, CancellationToken cancellationToken)
        {
            var node = await JsonNode.ParseAsync(stream, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return node ?? throw new EventUpcastingException("Event data stream contained no JSON content.");
        }

        private static JsonNode ParseOrThrow(string json)
        {
            return JsonNode.Parse(json)
                ?? throw new EventUpcastingException("Event data contained no JSON content.");
        }

        private static JsonNode ParseOrThrow(byte[] bytes)
        {
            return JsonNode.Parse(bytes)
                ?? throw new EventUpcastingException("Event data contained no JSON content.");
        }

        private static JsonSerializerOptions GetJsonSerializerOptions(string contentType)
        {
            // CloudEvents JSON uses the structured content mode; indent for readability is optional.
            return new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }
    }
}
