// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using CloudNative.CloudEvents;
using CloudNative.CloudEvents.SystemTextJson;

using Microsoft.Extensions.Options;

using System.Text;
using System.Text.Json;

namespace Deveel.Events
{
    /// <summary>
    /// Unit tests for <see cref="RabbitMqMessageFactory"/> that do not require
    /// a live RabbitMQ broker.
    /// </summary>
    [Trait("Channel", "RabbitMQ")]
    [Trait("Function", "MessageFactory")]
    public static class RabbitMqMessageFactoryTests
    {
        private static RabbitMqMessageFactory CreateFactory(
            RabbitMqMessageContent? content = null,
            RabbitMqMessageFormat? format = null)
        {
            var options = new RabbitMqPublishOptions
            {
                MessageContent = content,
                MessageFormat = format,
            };
            return new RabbitMqMessageFactory(Options.Create(options));
        }

        private static CloudEvent MakeJsonEvent() => new CloudEvent
        {
            Type = "order.placed",
            Source = new Uri("https://api.example.com"),
            Id = Guid.NewGuid().ToString("N"),
            Time = DateTimeOffset.UtcNow,
            DataContentType = "application/json",
            Data = JsonSerializer.Serialize(new { OrderId = "O-001", Total = 99.99 }),
        };

        // ── CloudEvent / Json (defaults) ──────────────────────────────────────

        [Fact]
        public static void CreateMessage_Defaults_ContentTypeIsCloudEventsJson()
        {
            var factory = CreateFactory();
            var message = factory.CreateMessage(MakeJsonEvent());

            Assert.Equal("application/cloudevents+json", message.ContentType);
        }

        [Fact]
        public static void CreateMessage_Defaults_EncodingIsUtf8()
        {
            var factory = CreateFactory();
            var message = factory.CreateMessage(MakeJsonEvent());

            Assert.Equal("utf-8", message.ContentEncoding);
        }

        [Fact]
        public static void CreateMessage_Defaults_BodyIsNonEmpty()
        {
            var factory = CreateFactory();
            var message = factory.CreateMessage(MakeJsonEvent());

            Assert.True(message.Body.Length > 0);
        }

        [Fact]
        public static void CreateMessage_CloudEventJson_BodyDeserializesToCloudEvent()
        {
            var factory = CreateFactory(
                content: RabbitMqMessageContent.CloudEvent,
                format: RabbitMqMessageFormat.Json);

            var original = MakeJsonEvent();
            var message = factory.CreateMessage(original);

            var json = JsonSerializer.Deserialize<JsonElement>(message.Body.Span);
            var formatter = new JsonEventFormatter();
            var decoded = formatter.ConvertFromJsonElement(json, null);

            Assert.Equal(original.Id, decoded.Id);
            Assert.Equal(original.Type, decoded.Type);
            Assert.Equal(original.Source, decoded.Source);
        }

        // ── EventData / Json ─────────────────────────────────────────────────

        [Fact]
        public static void CreateMessage_EventDataJson_ContentTypeIsApplicationJson()
        {
            var factory = CreateFactory(
                content: RabbitMqMessageContent.EventData,
                format: RabbitMqMessageFormat.Json);

            var message = factory.CreateMessage(MakeJsonEvent());

            Assert.Equal("application/json", message.ContentType);
        }

        [Fact]
        public static void CreateMessage_EventDataJson_EncodingIsUtf8()
        {
            var factory = CreateFactory(
                content: RabbitMqMessageContent.EventData,
                format: RabbitMqMessageFormat.Json);

            var message = factory.CreateMessage(MakeJsonEvent());

            Assert.Equal("utf-8", message.ContentEncoding);
        }

        [Fact]
        public static void CreateMessage_EventDataJson_BodyIsNonEmpty()
        {
            var factory = CreateFactory(
                content: RabbitMqMessageContent.EventData,
                format: RabbitMqMessageFormat.Json);

            var message = factory.CreateMessage(MakeJsonEvent());

            Assert.True(message.Body.Length > 0);
        }

        // ── CloudEvent / Binary ───────────────────────────────────────────────

        [Fact]
        public static void CreateMessage_CloudEventBinary_ThrowsNotSupported()
        {
            var factory = CreateFactory(
                content: RabbitMqMessageContent.CloudEvent,
                format: RabbitMqMessageFormat.Binary);

            Assert.Throws<NotSupportedException>(() => factory.CreateMessage(MakeJsonEvent()));
        }

        // ── EventData / Binary — body is non-empty ────────────────────────────

        [Fact]
        public static void CreateMessage_EventDataBinary_BodyIsNonEmpty()
        {
            var factory = CreateFactory(
                content: RabbitMqMessageContent.EventData,
                format: RabbitMqMessageFormat.Binary);

            var message = factory.CreateMessage(MakeJsonEvent());

            Assert.True(message.Body.Length > 0);
        }

        // ── Null event data ───────────────────────────────────────────────────

        [Fact]
        public static void CreateMessage_EventDataJson_WithNullData_BodyIsNullJson()
        {
            var factory = CreateFactory(
                content: RabbitMqMessageContent.EventData,
                format: RabbitMqMessageFormat.Json);

            var nullDataEvent = new CloudEvent
            {
                Type   = "order.cancelled",
                Source = new Uri("https://api.example.com"),
                Id     = Guid.NewGuid().ToString("N"),
                Time   = DateTimeOffset.UtcNow,
                // Data is intentionally left null
            };

            var message = factory.CreateMessage(nullDataEvent);

            // JsonSerializer.Serialize(null) produces "null"
            var bodyText = System.Text.Encoding.UTF8.GetString(message.Body.Span);
            Assert.Equal("null", bodyText);
        }

        // ── ContentType round-trip ────────────────────────────────────────────

        [Fact]
        public static void CreateMessage_CloudEventJson_ContentTypeRoundTrips()
        {
            var factory = CreateFactory(
                content: RabbitMqMessageContent.CloudEvent,
                format: RabbitMqMessageFormat.Json);

            var message = factory.CreateMessage(MakeJsonEvent());

            Assert.StartsWith("application/cloudevents", message.ContentType);
        }

        // ── EventData / Binary ─────────────────────────────────────────────────

        [Fact]
        public static void CreateMessage_EventDataBinary_ContentTypeIsOctetStream()
        {
            var factory = CreateFactory(
                content: RabbitMqMessageContent.EventData,
                format: RabbitMqMessageFormat.Binary);

            var message = factory.CreateMessage(MakeJsonEvent());

            Assert.Equal("application/octet-stream", message.ContentType);
        }

        [Fact]
        public static void CreateMessage_EventDataBinary_EncodingIsNull()
        {
            var factory = CreateFactory(
                content: RabbitMqMessageContent.EventData,
                format: RabbitMqMessageFormat.Binary);

            var message = factory.CreateMessage(MakeJsonEvent());

            Assert.Null(message.ContentEncoding);
        }
    }
}

