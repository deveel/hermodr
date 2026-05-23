// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using CloudNative.CloudEvents;

using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace Hermodr
{
    public static class EventSerializerTests
    {
        private static CloudEvent MakeEvent(string? type = "test.event") => new()
        {
            Type = type,
            Source = new Uri("https://api.example.com"),
            Id = "abc-123",
            DataContentType = "application/json",
            Data = JsonSerializer.Serialize(new { name = "Alice", age = 30 }),
            Time = DateTimeOffset.Parse("2024-06-01T12:00:00Z"),
            DataSchema = new Uri("https://example.com/schema/1.0"),
            Subject = "test-subject"
        };

        private static CloudEvent MakeEventWithExtension() {
            var @event = MakeEvent();
            @event[CloudEventAttribute.CreateExtension("env", CloudEventAttributeType.String)] = "prod";
            return @event;
        }

        // ─── CloudEventsJsonSerializer ────────────────────────────────────────

        [Fact]
        public static void CloudEventsJson_Format_IsCloudEventsJson() {
            Assert.Equal(EventMessageFormat.CloudEventsJson, CloudEventsJsonSerializer.Default.Format);
        }

        [Fact]
        public static void CloudEventsJson_ContentType_StartsWithCloudEventsJson() {
            Assert.StartsWith("application/cloudevents+json", CloudEventsJsonSerializer.Default.ContentType);
        }

        [Fact]
        public static void CloudEventsJson_BatchContentType_StartsWithBatch() {
            Assert.StartsWith("application/cloudevents-batch+json", CloudEventsJsonSerializer.Default.BatchContentType);
        }

        [Fact]
        public static void CloudEventsJson_Serialize_ProducesValidJson() {
            var bytes = CloudEventsJsonSerializer.Default.Serialize(MakeEvent());
            var json = JsonDocument.Parse(bytes);
            Assert.Equal("test.event", json.RootElement.GetProperty("type").GetString());
            Assert.Equal("abc-123", json.RootElement.GetProperty("id").GetString());
        }

        [Fact]
        public static void CloudEventsJson_SerializeBatch_ProducesValidJsonArray() {
            var events = new List<CloudEvent> { MakeEvent("event.one"), MakeEvent("event.two") };
            var bytes = CloudEventsJsonSerializer.Default.SerializeBatch(events);
            var json = JsonDocument.Parse(bytes);
            Assert.Equal(JsonValueKind.Array, json.RootElement.ValueKind);
            Assert.Equal(2, json.RootElement.GetArrayLength());
        }

        // ─── CloudEventsXmlSerializer ─────────────────────────────────────────

        [Fact]
        public static void CloudEventsXml_Format_IsCloudEventsXml() {
            Assert.Equal(EventMessageFormat.CloudEventsXml, CloudEventsXmlSerializer.Default.Format);
        }

        [Fact]
        public static void CloudEventsXml_ContentType_StartsWithCloudEventsXml() {
            Assert.StartsWith("application/cloudevents+xml", CloudEventsXmlSerializer.Default.ContentType);
        }

        [Fact]
        public static void CloudEventsXml_Serialize_ProducesValidXml() {
            var bytes = CloudEventsXmlSerializer.Default.Serialize(MakeEvent());
            var xml = XDocument.Load(new MemoryStream(bytes));
            XNamespace ns = "http://cloudevents.io/xmlformat/V1";
            Assert.Equal(ns + "cloudevent", xml.Root!.Name);
            Assert.Equal("test.event", xml.Root.Element(ns + "type")!.Value);
            Assert.Equal("abc-123", xml.Root.Element(ns + "id")!.Value);
        }

        [Fact]
        public static void CloudEventsXml_Serialize_IncludesAllAttributes() {
            var bytes = CloudEventsXmlSerializer.Default.Serialize(MakeEvent());
            var xml = XDocument.Load(new MemoryStream(bytes));
            XNamespace ns = "http://cloudevents.io/xmlformat/V1";
            var root = xml.Root!;
            Assert.NotNull(root.Element(ns + "source"));
            Assert.NotNull(root.Element(ns + "time"));
            Assert.NotNull(root.Element(ns + "datacontenttype"));
            Assert.NotNull(root.Element(ns + "dataschema"));
            Assert.NotNull(root.Element(ns + "subject"));
            Assert.NotNull(root.Element(ns + "data"));
        }

        [Fact]
        public static void CloudEventsXml_Serialize_WithExtensionAttributes() {
            var bytes = CloudEventsXmlSerializer.Default.Serialize(MakeEventWithExtension());
            var xml = XDocument.Load(new MemoryStream(bytes));
            XNamespace ns = "http://cloudevents.io/xmlformat/V1";
            var extensions = xml.Root!.Element(ns + "extensions");
            Assert.NotNull(extensions);
            var envEl = extensions!.Elements(ns + "extension")
                .FirstOrDefault(e => e.Attribute("name")?.Value == "env");
            Assert.NotNull(envEl);
            Assert.Equal("prod", envEl!.Value);
        }

        [Fact]
        public static void CloudEventsXml_SerializeBatch_ProducesXmlWithMultipleEvents() {
            var events = new List<CloudEvent> { MakeEvent("event.one"), MakeEvent("event.two") };
            var bytes = CloudEventsXmlSerializer.Default.SerializeBatch(events);
            var xml = XDocument.Load(new MemoryStream(bytes));
            XNamespace ns = "http://cloudevents.io/xmlformat/V1";
            Assert.Equal(2, xml.Root!.Elements(ns + "cloudevent").Count());
        }

        [Fact]
        public static void CloudEventsXml_Serialize_EventWithNullData() {
            var @event = new CloudEvent
            {
                Type = "test.event",
                Source = new Uri("https://api.example.com"),
                Id = "no-data"
            };
            var bytes = CloudEventsXmlSerializer.Default.Serialize(@event);
            var xml = XDocument.Load(new MemoryStream(bytes));
            XNamespace ns = "http://cloudevents.io/xmlformat/V1";
            var dataEl = xml.Root!.Element(ns + "data");
            Assert.NotNull(dataEl);
        }

        // ─── JsonEventSerializer ──────────────────────────────────────────────

        [Fact]
        public static void Json_Format_IsJson() {
            Assert.Equal(EventMessageFormat.Json, JsonEventSerializer.Default.Format);
        }

        [Fact]
        public static void Json_ContentType_IsApplicationJson() {
            Assert.Equal("application/json; charset=utf-8", JsonEventSerializer.Default.ContentType);
        }

        [Fact]
        public static void Json_BatchContentType_IsApplicationJson() {
            Assert.Equal("application/json; charset=utf-8", JsonEventSerializer.Default.BatchContentType);
        }

        [Fact]
        public static void Json_Serialize_ProducesValidJson() {
            var bytes = JsonEventSerializer.Default.Serialize(MakeEvent());
            var json = JsonDocument.Parse(bytes);
            Assert.Equal("test.event", json.RootElement.GetProperty("type").GetString());
            Assert.Equal("abc-123", json.RootElement.GetProperty("id").GetString());
            Assert.StartsWith("https://api.example.com", json.RootElement.GetProperty("source").GetString());
        }

        [Fact]
        public static void Json_Serialize_IncludesAllCoreAttributes() {
            var bytes = JsonEventSerializer.Default.Serialize(MakeEvent());
            var json = JsonDocument.Parse(bytes);
            Assert.True(json.RootElement.TryGetProperty("specversion", out _));
            Assert.True(json.RootElement.TryGetProperty("time", out _));
            Assert.True(json.RootElement.TryGetProperty("datacontenttype", out _));
            Assert.True(json.RootElement.TryGetProperty("dataschema", out _));
            Assert.True(json.RootElement.TryGetProperty("subject", out _));
            Assert.True(json.RootElement.TryGetProperty("data", out _));
        }

        [Fact]
        public static void Json_Serialize_DataEmbeddedAsJson() {
            var bytes = JsonEventSerializer.Default.Serialize(MakeEvent());
            var json = JsonDocument.Parse(bytes);
            // Data should be a JSON object, not a quoted string
            var data = json.RootElement.GetProperty("data");
            Assert.Equal(JsonValueKind.Object, data.ValueKind);
        }

        [Fact]
        public static void Json_Serialize_WithExtensionAttributes() {
            var bytes = JsonEventSerializer.Default.Serialize(MakeEventWithExtension());
            var json = JsonDocument.Parse(bytes);
            Assert.True(json.RootElement.TryGetProperty("env", out var envProp));
            Assert.Equal("prod", envProp.GetString());
        }

        [Fact]
        public static void Json_SerializeBatch_ProducesJsonArray() {
            var events = new List<CloudEvent> { MakeEvent("event.one"), MakeEvent("event.two") };
            var bytes = JsonEventSerializer.Default.SerializeBatch(events);
            var json = JsonDocument.Parse(bytes);
            Assert.Equal(JsonValueKind.Array, json.RootElement.ValueKind);
            Assert.Equal(2, json.RootElement.GetArrayLength());
        }

        [Fact]
        public static void Json_Serialize_EventWithNonJsonStringData() {
            // Non-JSON string data should be embedded as plain string
            var @event = new CloudEvent
            {
                Type = "test.event",
                Source = new Uri("https://api.example.com"),
                Id = "plain-data",
                Data = "just-a-string-not-json",
            };
            var bytes = JsonEventSerializer.Default.Serialize(@event);
            var json = JsonDocument.Parse(bytes);
            var data = json.RootElement.GetProperty("data");
            Assert.Equal(JsonValueKind.String, data.ValueKind);
            Assert.Equal("just-a-string-not-json", data.GetString());
        }

        [Fact]
        public static void Json_Serialize_EventWithNonStringData() {
            // Non-string, non-null data uses ToString()
            var @event = new CloudEvent
            {
                Type = "test.event",
                Source = new Uri("https://api.example.com"),
                Id = "int-data",
                Data = 42, // int data
            };
            var bytes = JsonEventSerializer.Default.Serialize(@event);
            var json = JsonDocument.Parse(bytes);
            Assert.True(json.RootElement.TryGetProperty("data", out _));
        }

        // ─── XmlEventSerializer ───────────────────────────────────────────────

        private static readonly System.Xml.Linq.XNamespace XmlNs = "http://cloudevents.io/xmlformat/V1";

        [Fact]
        public static void Xml_Format_IsXml() {
            Assert.Equal(EventMessageFormat.Xml, XmlEventSerializer.Default.Format);
        }

        [Fact]
        public static void Xml_ContentType_IsApplicationXml() {
            Assert.Equal("application/xml; charset=utf-8", XmlEventSerializer.Default.ContentType);
        }

        [Fact]
        public static void Xml_BatchContentType_IsApplicationXml() {
            Assert.Equal("application/xml; charset=utf-8", XmlEventSerializer.Default.BatchContentType);
        }

        [Fact]
        public static void Xml_Serialize_ProducesValidXml() {
            var bytes = XmlEventSerializer.Default.Serialize(MakeEvent());
            var xml = XDocument.Load(new MemoryStream(bytes));
            Assert.Equal(XmlNs + "cloudevent", xml.Root!.Name);
            Assert.Equal("test.event", xml.Root.Element(XmlNs + "type")!.Value);
            Assert.Equal("abc-123", xml.Root.Element(XmlNs + "id")!.Value);
        }

        [Fact]
        public static void Xml_Serialize_IncludesAllCoreAttributes() {
            var bytes = XmlEventSerializer.Default.Serialize(MakeEvent());
            var xml = XDocument.Load(new MemoryStream(bytes));
            var root = xml.Root!;
            Assert.NotNull(root.Element(XmlNs + "source"));
            Assert.NotNull(root.Element(XmlNs + "time"));
            Assert.NotNull(root.Element(XmlNs + "datacontenttype"));
            Assert.NotNull(root.Element(XmlNs + "dataschema"));
            Assert.NotNull(root.Element(XmlNs + "subject"));
            Assert.NotNull(root.Element(XmlNs + "data"));
        }

        [Fact]
        public static void Xml_Serialize_WithExtensionAttributes() {
            var bytes = XmlEventSerializer.Default.Serialize(MakeEventWithExtension());
            var xml = XDocument.Load(new MemoryStream(bytes));
            var extensions = xml.Root!.Element(XmlNs + "extensions");
            Assert.NotNull(extensions);
            var envEl = extensions!.Elements(XmlNs + "extension")
                .FirstOrDefault(e => e.Attribute("name")?.Value == "env");
            Assert.NotNull(envEl);
        }

        [Fact]
        public static void Xml_SerializeBatch_ProducesXmlWithMultipleEvents() {
            var events = new List<CloudEvent> { MakeEvent("event.one"), MakeEvent("event.two") };
            var bytes = XmlEventSerializer.Default.SerializeBatch(events);
            var xml = XDocument.Load(new MemoryStream(bytes));
            Assert.Equal(2, xml.Root!.Elements(XmlNs + "cloudevent").Count());
        }

        [Fact]
        public static void Xml_Serialize_EventWithNullData() {
            var @event = new CloudEvent
            {
                Type = "test.event",
                Source = new Uri("https://api.example.com"),
                Id = "no-data"
            };
            var bytes = XmlEventSerializer.Default.Serialize(@event);
            var xml = XDocument.Load(new MemoryStream(bytes));
            var dataEl = xml.Root!.Element(XmlNs + "data");
            Assert.NotNull(dataEl);
        }

        [Fact]
        public static void Xml_Serialize_EventWithNonStringData() {
            var @event = new CloudEvent
            {
                Type = "test.event",
                Source = new Uri("https://api.example.com"),
                Id = "int-data",
                Data = 42, // non-string data
            };
            var bytes = XmlEventSerializer.Default.Serialize(@event);
            var xml = XDocument.Load(new MemoryStream(bytes));
            var dataEl = xml.Root!.Element(XmlNs + "data");
            Assert.NotNull(dataEl);
        }

        // ─── EventSerializerBase ──────────────────────────────────────────────

        [Fact]
        public static void SerializerBase_BatchContentType_FallsBackToContentType() {
            var s = new SimpleTestSerializer();
            Assert.Equal(s.ContentType, s.BatchContentType);
        }

        [Fact]
        public static void SerializerBase_SerializeBatch_ThrowsNotSupported() {
            var s = new SimpleTestSerializer();
            Assert.Throws<NotSupportedException>(() => s.SerializeBatch(new List<CloudEvent> { MakeEvent() }));
        }

        private sealed class SimpleTestSerializer : EventSerializerBase
        {
            public override string Format => "test/format";
            public override string ContentType => "application/test; charset=utf-8";
            public override byte[] Serialize(CloudEvent @event) => [];
        }
    }
}



