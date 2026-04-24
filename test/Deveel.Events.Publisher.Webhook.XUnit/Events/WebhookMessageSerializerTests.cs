//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace Deveel.Events
{
    public class WebhookMessageSerializerTests
    {
        private static CloudEvent MakeEvent(string type = "test.event") => new()
        {
            Type    = type,
            Source  = new Uri("https://api.example.com"),
            Id      = "abc123",
            DataContentType = "application/json",
            Data    = JsonSerializer.Serialize(new { name = "Alice" }),
            Time    = DateTimeOffset.Parse("2024-06-01T12:00:00Z"),
        };

        private static IReadOnlyList<CloudEvent> MakeBatch() =>
            new[] { MakeEvent("event.one"), MakeEvent("event.two") };

        // ── JSON ──────────────────────────────────────────────────────────────

        [Fact]
        public void Json_ContentType_IsCloudEventsJson()
        {
            var s = CloudEventsJsonSerializer.Default;
            Assert.Equal(EventMessageFormat.CloudEventsJson, s.Format);
            Assert.StartsWith("application/cloudevents+json", s.ContentType);
        }

        [Fact]
        public void Json_Serialize_ProducesValidJson()
        {
            var bytes = CloudEventsJsonSerializer.Default.Serialize(MakeEvent());
            var json  = JsonDocument.Parse(bytes);
            Assert.Equal("test.event", json.RootElement.GetProperty("type").GetString());
            Assert.Equal("abc123",     json.RootElement.GetProperty("id").GetString());
        }

        // ── XML ───────────────────────────────────────────────────────────────

        [Fact]
        public void Xml_ContentType_IsCloudEventsXml()
        {
            var s = CloudEventsXmlSerializer.Default;
            Assert.Equal(EventMessageFormat.CloudEventsXml, s.Format);
            Assert.StartsWith("application/cloudevents+xml", s.ContentType);
        }

        [Fact]
        public void Xml_Serialize_ProducesValidXml()
        {
            var bytes = CloudEventsXmlSerializer.Default.Serialize(MakeEvent());
            var xml   = XDocument.Load(new MemoryStream(bytes));

            XNamespace ns = "http://cloudevents.io/xmlformat/V1";
            var root  = xml.Root!;

            Assert.Equal(ns + "cloudevent", root.Name);
            Assert.Equal("test.event",      root.Element(ns + "type")!.Value);
            Assert.Equal("abc123",          root.Element(ns + "id")!.Value);
            Assert.NotNull(root.Element(ns + "data"));
        }

        [Fact]
        public void Xml_Serialize_IncludesSpecVersion()
        {
            var bytes = CloudEventsXmlSerializer.Default.Serialize(MakeEvent());
            var xml   = XDocument.Load(new MemoryStream(bytes));
            var attr  = xml.Root!.Attribute("specversion");
            Assert.NotNull(attr);
            Assert.Equal("1.0", attr!.Value);
        }

        [Fact]
        public void Xml_Serialize_IncludesExtensionAttributes()
        {
            var @event = MakeEvent();
            @event[CloudEventAttribute.CreateExtension("env", CloudEventAttributeType.String)] = "prod";

            var bytes = CloudEventsXmlSerializer.Default.Serialize(@event);
            var xml   = XDocument.Load(new MemoryStream(bytes));

            XNamespace ns = "http://cloudevents.io/xmlformat/V1";
            var ext   = xml.Root!.Element(ns + "extensions");
            Assert.NotNull(ext);

            var envEl = ext!.Elements(ns + "extension")
                            .FirstOrDefault(e => e.Attribute("name")?.Value == "env");
            Assert.NotNull(envEl);
            Assert.Equal("prod", envEl!.Value);
        }

        // ── CloudEventsJson batch ─────────────────────────────────────────────

        [Fact]
        public void CloudEventsJson_SerializeBatch_ProducesJsonArray()
        {
            var bytes = CloudEventsJsonSerializer.Default.SerializeBatch(MakeBatch());
            var json  = JsonDocument.Parse(bytes);
            Assert.Equal(System.Text.Json.JsonValueKind.Array, json.RootElement.ValueKind);
            Assert.Equal(2, json.RootElement.GetArrayLength());
        }

        [Fact]
        public void CloudEventsJson_BatchContentType_IsCorrect()
        {
            Assert.StartsWith("application/cloudevents-batch+json", CloudEventsJsonSerializer.Default.BatchContentType);
        }

        // ── CloudEventsXml batch ──────────────────────────────────────────────

        [Fact]
        public void CloudEventsXml_SerializeBatch_ProducesXmlWithMultipleEvents()
        {
            var bytes = CloudEventsXmlSerializer.Default.SerializeBatch(MakeBatch());
            var xml   = XDocument.Load(new MemoryStream(bytes));
            XNamespace ns = "http://cloudevents.io/xmlformat/V1";
            Assert.Equal(2, xml.Root!.Elements(ns + "cloudevent").Count());
        }
    }
}
