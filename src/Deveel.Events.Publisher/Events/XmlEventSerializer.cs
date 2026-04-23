//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

using System.Xml.Linq;

namespace Deveel.Events
{
    /// <summary>
    /// Serializes <see cref="CloudEvent"/> instances as plain
    /// <c>application/xml</c> messages.
    /// </summary>
    /// <remarks>
    /// Each event is rendered as a <c>&lt;cloudevent&gt;</c> XML element inside
    /// the <c>http://cloudevents.io/xmlformat/V1</c> namespace. Core context
    /// attributes (<c>id</c>, <c>type</c>, <c>specversion</c>, …) are mapped to
    /// child elements; extension attributes are grouped under an
    /// <c>&lt;extensions&gt;</c> element; the event data is placed inside a
    /// <c>&lt;data&gt;</c> CDATA section.
    /// <br/>
    /// Batch deliveries wrap multiple <c>&lt;cloudevent&gt;</c> elements inside a
    /// root <c>&lt;events&gt;</c> element.
    /// </remarks>
    public class XmlEventSerializer : IEventSerializer
    {
        internal static readonly XNamespace Ns = "http://cloudevents.io/xmlformat/V1";

        private static readonly HashSet<string> StandardAttributes =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "specversion", "id", "type", "source", "time",
                "datacontenttype", "dataschema", "subject", "data"
            };

        /// <summary>A shared singleton instance.</summary>
        public static readonly XmlEventSerializer Default = new();

        /// <inheritdoc/>
        public string Format => EventMessageFormat.Xml;

        /// <inheritdoc/>
        public string ContentType => "application/xml; charset=utf-8";

        /// <inheritdoc/>
        public string BatchContentType => "application/xml; charset=utf-8";

        /// <inheritdoc/>
        public byte[] Serialize(CloudEvent @event)
        {
            var doc = new XDocument(new XDeclaration("1.0", "utf-8", null), BuildEventElement(@event));
            using var ms = new MemoryStream();
            doc.Save(ms);
            return ms.ToArray();
        }

        /// <inheritdoc/>
        public byte[] SerializeBatch(IReadOnlyList<CloudEvent> events)
        {
            var root = new XElement(Ns + "events");
            foreach (var e in events)
                root.Add(BuildEventElement(e));

            var doc = new XDocument(new XDeclaration("1.0", "utf-8", null), root);
            using var ms = new MemoryStream();
            doc.Save(ms);
            return ms.ToArray();
        }

        private static XElement BuildEventElement(CloudEvent @event)
        {
            var el = new XElement(Ns + "cloudevent",
                new XAttribute("specversion", @event.SpecVersion.VersionId));

            void AddChild(string name, string? value) {
                if (value != null) el.Add(new XElement(Ns + name, value));
            }

            AddChild("type",            @event.Type);
            AddChild("source",          @event.Source?.ToString());
            AddChild("id",              @event.Id);
            if (@event.Time.HasValue)
                AddChild("time", @event.Time.Value.ToString("O"));
            AddChild("datacontenttype", @event.DataContentType);
            AddChild("dataschema",      @event.DataSchema?.ToString());
            AddChild("subject",         @event.Subject);

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
                el.Add(extEl);
            }

            // Data
            var dataEl = new XElement(Ns + "data");
            if (@event.Data is string s) dataEl.Add(new XCData(s));
            else if (@event.Data != null) dataEl.Add(new XCData(@event.Data.ToString() ?? string.Empty));
            el.Add(dataEl);

            return el;
        }
    }
}

