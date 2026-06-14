//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Text.Json;

namespace Hermodr
{
    /// <summary>
    /// Deserializes JSON event data into a format-agnostic object tree.
    /// </summary>
    public sealed class JsonEventDataDeserializer : IEventDataDeserializer
    {
        /// <inheritdoc/>
        public string ContentType => "application/json";

        /// <inheritdoc/>
        public bool CanDeserialize(string contentType)
        {
            if (string.IsNullOrEmpty(contentType))
                return false;

            return contentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase)
                || contentType.StartsWith("application/cloudevents+json", StringComparison.OrdinalIgnoreCase);
        }

        /// <inheritdoc/>
        public async Task<object?> DeserializeAsync(Stream data, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(data);

            var jsonDoc = await JsonDocument.ParseAsync(data, cancellationToken: cancellationToken);
            return ConvertJsonElementToObject(jsonDoc.RootElement);
        }

        private static object? ConvertJsonElementToObject(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Null => null,
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Array => ConvertJsonArrayToList(element),
                JsonValueKind.Object => ConvertJsonObjectToDictionary(element),
                _ => null
            };
        }

        private static List<object?> ConvertJsonArrayToList(JsonElement arrayElement)
        {
            var list = new List<object?>();
            foreach (var item in arrayElement.EnumerateArray())
            {
                list.Add(ConvertJsonElementToObject(item));
            }
            return list;
        }

        private static Dictionary<string, object?> ConvertJsonObjectToDictionary(JsonElement objElement)
        {
            var dict = new Dictionary<string, object?>();
            foreach (var property in objElement.EnumerateObject())
            {
                dict[property.Name] = ConvertJsonElementToObject(property.Value);
            }
            return dict;
        }
    }
}
