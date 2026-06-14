//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Xml.Linq;

namespace Hermodr
{
    /// <summary>
    /// Deserializes XML event data into a format-agnostic object tree.
    /// </summary>
    public sealed class XmlEventDataDeserializer : IEventDataDeserializer
    {
        /// <inheritdoc/>
        public string ContentType => "application/xml";

        /// <inheritdoc/>
        public bool CanDeserialize(string contentType)
        {
            if (string.IsNullOrEmpty(contentType))
                return false;

            return contentType.StartsWith("application/xml", StringComparison.OrdinalIgnoreCase);
        }

        /// <inheritdoc/>
        public Task<object?> DeserializeAsync(Stream data, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(data);

            cancellationToken.ThrowIfCancellationRequested();

            var doc = XDocument.Load(data);
            var result = ConvertXElementToDictionary(doc.Root);
            return Task.FromResult<object?>(result);
        }

        private static Dictionary<string, object?> ConvertXElementToDictionary(XElement element)
        {
            var dict = new Dictionary<string, object?>();

            // Add attributes as properties (attributes take precedence)
            foreach (var attr in element.Attributes())
            {
                // Ignore namespaces, use local name only
                dict[attr.Name.LocalName] = attr.Value;
            }

            // Add child elements as properties
            foreach (var child in element.Elements())
            {
                // Ignore namespaces, use local name only
                var key = child.Name.LocalName;

                // If attribute already exists with same name, element doesn't override
                if (!dict.ContainsKey(key))
                {
                    dict[key] = ConvertXElementToObject(child);
                }
            }

            return dict;
        }

        private static object? ConvertXElementToObject(XElement element)
        {
            // If element has child elements, convert to dictionary
            if (element.HasElements)
            {
                return ConvertXElementToDictionary(element);
            }

            // Otherwise, return the text value
            return element.Value;
        }
    }
}
