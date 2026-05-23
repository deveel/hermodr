//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

using System.Xml.Linq;

namespace Hermodr
{
    /// <summary>
    /// Serializes <see cref="CloudEvent"/> instances in the CloudEvents
    /// structured XML format (<c>application/cloudevents+xml</c>).
    /// </summary>
    public class CloudEventsXmlSerializer : IEventSerializer
    {
        private static readonly XNamespace Ns = "http://cloudevents.io/xmlformat/V1";

        private static readonly HashSet<string> StandardAttributes =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "specversion", "type", "source", "id", "time",
                "datacontenttype", "dataschema", "subject", "data"
            };

        /// <summary>A shared singleton instance.</summary>
        public static readonly CloudEventsXmlSerializer Default = new();

        /// <inheritdoc/>
        public string Format => EventMessageFormat.CloudEventsXml;

        /// <inheritdoc/>
        public string ContentType => "application/cloudevents+xml; charset=utf-8";

        /// <inheritdoc/>
        public string BatchContentType => "application/cloudevents+xml; charset=utf-8";

        /// <inheritdoc/>
        public byte[] Serialize(CloudEvent @event)
        {
            var doc = new XDocument(new XDeclaration("1.0", "utf-8", null), BuildCloudEventElement(@event));
            using var ms = new MemoryStream();
            doc.Save(ms);
            return ms.ToArray();
        }

        /// <inheritdoc/>
        public byte[] SerializeBatch(IReadOnlyList<CloudEvent> events)
        {
            var root = new XElement(Ns + "events");
            foreach (var e in events)
                root.Add(BuildCloudEventElement(e));
            var doc = new XDocument(new XDeclaration("1.0", "utf-8", null), root);
            using var ms = new MemoryStream();
            doc.Save(ms);
            return ms.ToArray();
        }

        private static XElement BuildCloudEventElement(CloudEvent @event)
        {
            var root = new XElement(Ns + "cloudevent",
                new XAttribute("specversion", @event.SpecVersion.VersionId));

            void Add(string name, string? value) {
                if (value != null) root.Add(new XElement(Ns + name, value));
            }

            Add("type",            @event.Type);
            Add("source",          @event.Source?.ToString());
            Add("id",              @event.Id);
            if (@event.Time.HasValue) Add("time", @event.Time.Value.ToString("O"));
            Add("datacontenttype", @event.DataContentType);
            Add("dataschema",      @event.DataSchema?.ToString());
            Add("subject",         @event.Subject);

            // Extension attributes
            var extensions = @event.GetPopulatedAttributes()
                .Where(a => !StandardAttributes.Contains(a.Key.Name)).ToList();
            if (extensions.Count > 0)
            {
                var extEl = new XElement(Ns + "extensions");
                foreach (var (attr, value) in extensions)
                    extEl.Add(new XElement(Ns + "extension",
                        new XAttribute("name", attr.Name),
                        value?.ToString() ?? string.Empty));
                root.Add(extEl);
            }

            // Data
            var dataEl = new XElement(Ns + "data");
            if (@event.Data is string s) dataEl.Add(new XCData(s));
            else if (@event.Data != null) dataEl.Add(new XCData(@event.Data.ToString() ?? string.Empty));
            root.Add(dataEl);

            return root;
        }
    }
}

