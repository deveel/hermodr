// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System.Text.Json;

using CloudNative.CloudEvents;

namespace Deveel.Events
{
    /// <summary>
    /// Unit tests for <see cref="JsonEventDataDeserializer"/> covering all
    /// resolution branches in <see cref="JsonEventDataDeserializer.TryGetJsonElement"/>
    /// and both the static helpers and the <see cref="IEventDataDeserializer"/> instance
    /// surface.
    /// </summary>
    [Trait("Feature", "Subscriptions")]
    [Trait("Concern", "JsonDeserializer")]
    public static class JsonEventDataDeserializerTests
    {
        // ── helpers ────────────────────────────────────────────────────────────

        private static CloudEvent MakeEvent(object? data, string? contentType = "application/json")
        {
            var e = new CloudEvent
            {
                Type            = "com.example.test",
                Source          = new Uri("https://example.com"),
                Id              = Guid.NewGuid().ToString("N"),
                DataContentType = contentType,
                Data            = data
            };
            return e;
        }

        // ── IsJsonContent ──────────────────────────────────────────────────────

        [Fact]
        public static void IsJsonContent_JsonContentType_ReturnsTrue()
        {
            Assert.True(JsonEventDataDeserializer.IsJsonContent(MakeEvent(null, "application/json")));
        }

        [Fact]
        public static void IsJsonContent_VndJsonContentType_ReturnsTrue()
        {
            Assert.True(JsonEventDataDeserializer.IsJsonContent(MakeEvent(null, "application/vnd.example+json")));
        }

        [Fact]
        public static void IsJsonContent_NullContentType_ReturnsFalse()
        {
            Assert.False(JsonEventDataDeserializer.IsJsonContent(MakeEvent(null, null)));
        }

        [Fact]
        public static void IsJsonContent_PlainTextContentType_ReturnsFalse()
        {
            Assert.False(JsonEventDataDeserializer.IsJsonContent(MakeEvent(null, "text/plain")));
        }

        // ── CanDeserialize (instance method) ──────────────────────────────────

        [Fact]
        public static void CanDeserialize_JsonContentType_ReturnsTrue()
        {
            Assert.True(JsonEventDataDeserializer.Instance.CanDeserialize("application/json"));
        }

        [Fact]
        public static void CanDeserialize_CloudEventsJsonContentType_ReturnsTrue()
        {
            Assert.True(JsonEventDataDeserializer.Instance.CanDeserialize("application/cloudevents+json"));
        }

        [Fact]
        public static void CanDeserialize_NullContentType_ReturnsFalse()
        {
            Assert.False(JsonEventDataDeserializer.Instance.CanDeserialize(null));
        }

        [Fact]
        public static void CanDeserialize_NonJsonContentType_ReturnsFalse()
        {
            Assert.False(JsonEventDataDeserializer.Instance.CanDeserialize("application/xml"));
        }

        // ── TryGetJsonElement: JsonElement data ───────────────────────────────

        [Fact]
        public static void TryGetJsonElement_JsonElementData_ReturnsAsIs()
        {
            using var doc = JsonDocument.Parse("""{"status":"active"}""");
            var je    = doc.RootElement.Clone();
            var @event = MakeEvent(je);

            var result = JsonEventDataDeserializer.TryGetJsonElement(@event, out var element);

            Assert.True(result);
            Assert.Equal(JsonValueKind.Object, element.ValueKind);
            Assert.Equal("active", element.GetProperty("status").GetString());
        }

        // ── TryGetJsonElement: string data with JSON content type ─────────────

        [Fact]
        public static void TryGetJsonElement_JsonString_JsonContentType_ReturnsParsed()
        {
            var @event = MakeEvent("""{"key":"value"}""");

            var result = JsonEventDataDeserializer.TryGetJsonElement(@event, out var element);

            Assert.True(result);
            Assert.Equal("value", element.GetProperty("key").GetString());
        }

        [Fact]
        public static void TryGetJsonElement_InvalidJsonString_JsonContentType_ReturnsFalse()
        {
            var @event = MakeEvent("not valid json");

            var result = JsonEventDataDeserializer.TryGetJsonElement(@event, out _);

            Assert.False(result);
        }

        [Fact]
        public static void TryGetJsonElement_JsonString_NonJsonContentType_ReturnsFalse()
        {
            // String data but non-JSON content type should NOT match the "string" branch
            // because IsJsonContent is false.
            var @event = MakeEvent("""{"key":"value"}""", "text/plain");

            var result = JsonEventDataDeserializer.TryGetJsonElement(@event, out _);

            Assert.False(result);
        }

        // ── TryGetJsonElement: binary / stream / null data ────────────────────

        [Fact]
        public static void TryGetJsonElement_ByteArrayData_ReturnsFalse()
        {
            var @event = MakeEvent(new byte[] { 1, 2, 3 });

            var result = JsonEventDataDeserializer.TryGetJsonElement(@event, out _);

            Assert.False(result);
        }

        [Fact]
        public static void TryGetJsonElement_StreamData_ReturnsFalse()
        {
            var @event = MakeEvent(new MemoryStream([1, 2, 3]));

            var result = JsonEventDataDeserializer.TryGetJsonElement(@event, out _);

            Assert.False(result);
        }

        [Fact]
        public static void TryGetJsonElement_NullData_ReturnsFalse()
        {
            var @event = MakeEvent(null);

            var result = JsonEventDataDeserializer.TryGetJsonElement(@event, out _);

            Assert.False(result);
        }

        // ── TryGetJsonElement: arbitrary object with JSON content type ─────────

        [Fact]
        public static void TryGetJsonElement_ObjectData_JsonContentType_SerializesAndParses()
        {
            var data   = new { name = "Alice", age = 30 };
            var @event = MakeEvent(data);

            var result = JsonEventDataDeserializer.TryGetJsonElement(@event, out var element);

            Assert.True(result);
            Assert.Equal("Alice", element.GetProperty("name").GetString());
            Assert.Equal(30, element.GetProperty("age").GetInt32());
        }

        [Fact]
        public static void TryGetJsonElement_ObjectData_NonJsonContentType_ReturnsFalse()
        {
            // Object data but non-JSON content type → IsJsonContent is false → returns false
            var data   = new { name = "Alice" };
            var @event = MakeEvent(data, "text/plain");

            var result = JsonEventDataDeserializer.TryGetJsonElement(@event, out _);

            Assert.False(result);
        }

        // ── IEventDataDeserializer.TryDeserialize (instance wrapper) ─────────

        [Fact]
        public static void TryDeserialize_Instance_JsonElementData_ReturnsTrue()
        {
            using var doc = JsonDocument.Parse("""{"x":1}""");
            var je    = doc.RootElement.Clone();
            var @event = MakeEvent(je);

            var result = JsonEventDataDeserializer.Instance.TryDeserialize(@event, out var element);

            Assert.True(result);
            Assert.Equal(1, element.GetProperty("x").GetInt32());
        }

        [Fact]
        public static void TryDeserialize_Instance_NullData_ReturnsFalse()
        {
            var @event = MakeEvent(null);

            var result = JsonEventDataDeserializer.Instance.TryDeserialize(@event, out _);

            Assert.False(result);
        }
    }
}

