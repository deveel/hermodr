// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System.Text;
using System.Text.Json;

using Azure.Messaging.ServiceBus;

using CloudNative.CloudEvents;

namespace Deveel.Events
{
    /// <summary>
    /// Unit tests for <see cref="ServiceBusMessageFactory"/> that do not require
    /// a live Azure Service Bus connection.
    /// </summary>
    [Trait("Channel", "ServiceBus")]
    [Trait("Function", "MessageFactory")]
    public static class ServiceBusMessageFactoryTests
    {
        private static readonly ServiceBusMessageFactory Factory = new ServiceBusMessageFactory();

        private static CloudEvent MakeJsonEvent(string? subject = "test") => new CloudEvent
        {
            Type = "order.placed",
            Source = new Uri("https://api.example.com"),
            Id = Guid.NewGuid().ToString("N"),
            Time = DateTimeOffset.UtcNow,
            Subject = subject,
            DataContentType = "application/json",
            DataSchema = new Uri("http://example.com/schema/order/1.0"),
            Data = "{\"orderId\": \"O-001\"}",
        };

        private static CloudEvent MakeBinaryEvent() => new CloudEvent
        {
            Type = "file.uploaded",
            Source = new Uri("https://api.example.com"),
            Id = Guid.NewGuid().ToString("N"),
            Time = DateTimeOffset.UtcNow,
            DataContentType = "application/binary",
            Data = Encoding.UTF8.GetBytes(Convert.ToBase64String(Encoding.UTF8.GetBytes("binary-payload"))),
        };

        // ── MessageId ────────────────────────────────────────────────────────

        [Fact]
        public static void CreateMessage_MessageIdEqualsEventId()
        {
            var @event = MakeJsonEvent();
            var message = Factory.CreateMessage(@event);

            Assert.Equal(@event.Id, message.MessageId);
        }

        // ── ContentType ───────────────────────────────────────────────────────

        [Fact]
        public static void CreateMessage_JsonData_ContentTypeIsApplicationJson()
        {
            var message = Factory.CreateMessage(MakeJsonEvent());

            Assert.Equal("application/json", message.ContentType);
        }

        [Fact]
        public static void CreateMessage_BinaryData_ContentTypeIsApplicationBinary()
        {
            var message = Factory.CreateMessage(MakeBinaryEvent());

            Assert.Equal("application/binary", message.ContentType);
        }

        // ── Subject ───────────────────────────────────────────────────────────

        [Fact]
        public static void CreateMessage_Subject_IsPreservedFromEvent()
        {
            var @event = MakeJsonEvent(subject: "my-subject");
            var message = Factory.CreateMessage(@event);

            Assert.Equal("my-subject", message.Subject);
        }

        [Fact]
        public static void CreateMessage_NullSubject_IsNull()
        {
            var @event = MakeJsonEvent(subject: null);
            var message = Factory.CreateMessage(@event);

            Assert.Null(message.Subject);
        }

        // ── Body ─────────────────────────────────────────────────────────────

        [Fact]
        public static void CreateMessage_JsonData_BodyIsNonEmpty()
        {
            var message = Factory.CreateMessage(MakeJsonEvent());

            Assert.NotNull(message.Body);
            Assert.True(message.Body.ToArray().Length > 0);
        }

        [Fact]
        public static void CreateMessage_BinaryData_BodyIsNonEmpty()
        {
            var message = Factory.CreateMessage(MakeBinaryEvent());

            Assert.NotNull(message.Body);
            Assert.True(message.Body.ToArray().Length > 0);
        }

        // ── Application properties ────────────────────────────────────────────

        [Fact]
        public static void CreateMessage_ApplicationProperties_ContainEventType()
        {
            var @event = MakeJsonEvent();
            var message = Factory.CreateMessage(@event);

            Assert.True(message.ApplicationProperties.ContainsKey(ServiceBusMessageProperties.EventType));
            Assert.Equal(@event.Type, message.ApplicationProperties[ServiceBusMessageProperties.EventType]);
        }

        [Fact]
        public static void CreateMessage_ApplicationProperties_ContainTimestamp()
        {
            var @event = MakeJsonEvent();
            var message = Factory.CreateMessage(@event);

            Assert.True(message.ApplicationProperties.ContainsKey(ServiceBusMessageProperties.TimeStamp));
        }

        [Fact]
        public static void CreateMessage_ApplicationProperties_ContainDataVersion()
        {
            var @event = MakeJsonEvent();
            var message = Factory.CreateMessage(@event);

            Assert.True(message.ApplicationProperties.ContainsKey(ServiceBusMessageProperties.DataVersion));
            Assert.Equal(@event.DataSchema!.ToString(), message.ApplicationProperties[ServiceBusMessageProperties.DataVersion]);
        }

        [Fact]
        public static void CreateMessage_NoDataSchema_DataVersionPropertyAbsent()
        {
            var @event = new CloudEvent
            {
                Type = "test.event",
                Source = new Uri("https://api.example.com"),
                Id = Guid.NewGuid().ToString("N"),
                Time = DateTimeOffset.UtcNow,
                DataContentType = "application/json",
                Data = "{}",
                DataSchema = null,
            };

            var message = Factory.CreateMessage(@event);

            Assert.False(message.ApplicationProperties.ContainsKey(ServiceBusMessageProperties.DataVersion));
        }

        // ── Null content type ─────────────────────────────────────────────────

        [Fact]
        public static void CreateMessage_NullContentType_BodyIsEmpty()
        {
            var @event = new CloudEvent
            {
                Type = "test.event",
                Source = new Uri("https://api.example.com"),
                Id = Guid.NewGuid().ToString("N"),
                Time = DateTimeOffset.UtcNow,
                DataContentType = null,
                Data = null,
            };

            var message = Factory.CreateMessage(@event);

            // With null content-type GetBinaryData returns null; ServiceBusMessage.Body
            // becomes BinaryData.Empty (not null), so we check the byte length.
            Assert.Equal(0, message.Body.ToArray().Length);
        }

        // ── Null data with valid content-type ─────────────────────────────────

        [Fact]
        public static void CreateMessage_NullData_ValidJsonContentType_BodyIsEmpty()
        {
            var @event = new CloudEvent
            {
                Type            = "test.event",
                Source          = new Uri("https://api.example.com"),
                Id              = Guid.NewGuid().ToString("N"),
                Time            = DateTimeOffset.UtcNow,
                DataContentType = "application/json",
                Data            = null,
            };

            var message = Factory.CreateMessage(@event);

            Assert.Equal(0, message.Body.ToArray().Length);
        }

        // ── application/octet-stream with byte array ──────────────────────────

        [Fact]
        public static void CreateMessage_OctetStream_ByteArray_BodyMatchesInput()
        {
            var payload = System.Text.Encoding.UTF8.GetBytes("raw-bytes");
            var @event = new CloudEvent
            {
                Type            = "file.uploaded",
                Source          = new Uri("https://api.example.com"),
                Id              = Guid.NewGuid().ToString("N"),
                Time            = DateTimeOffset.UtcNow,
                DataContentType = "application/octet-stream",
                Data            = payload,
            };

            var message = Factory.CreateMessage(@event);

            Assert.Equal(payload, message.Body.ToArray());
        }

        // ── application/octet-stream with base64 string ───────────────────────

        [Fact]
        public static void CreateMessage_OctetStream_Base64String_BodyIsDecodedBytes()
        {
            var original = "hello-octet";
            var base64   = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(original));
            var @event = new CloudEvent
            {
                Type            = "file.uploaded",
                Source          = new Uri("https://api.example.com"),
                Id              = Guid.NewGuid().ToString("N"),
                Time            = DateTimeOffset.UtcNow,
                DataContentType = "application/octet-stream",
                Data            = base64,
            };

            var message = Factory.CreateMessage(@event);

            var decoded = System.Text.Encoding.UTF8.GetString(message.Body.ToArray());
            Assert.Equal(original, decoded);
        }

        // ── Binary content type with invalid (non-byte[]/non-string) data ──────

        [Fact]
        public static void CreateMessage_BinaryContentType_InvalidDataType_ThrowsArgumentException()
        {
            var @event = new CloudEvent
            {
                Type            = "file.uploaded",
                Source          = new Uri("https://api.example.com"),
                Id              = Guid.NewGuid().ToString("N"),
                Time            = DateTimeOffset.UtcNow,
                DataContentType = "application/binary",
                Data            = 12345, // int is not a valid binary payload
            };

            Assert.Throws<ArgumentException>(() => Factory.CreateMessage(@event));
        }

        // ── JSON with a CLR object (not a string) should be serialized ─────────

        [Fact]
        public static void CreateMessage_JsonData_ObjectPayload_BodyIsSerializedJson()
        {
            var payload = new { orderId = "O-999", amount = 42.0 };
            var @event = new CloudEvent
            {
                Type            = "order.placed",
                Source          = new Uri("https://api.example.com"),
                Id              = Guid.NewGuid().ToString("N"),
                Time            = DateTimeOffset.UtcNow,
                DataContentType = "application/json",
                Data            = payload,
            };

            var message = Factory.CreateMessage(@event);

            var body = System.Text.Encoding.UTF8.GetString(message.Body.ToArray());
            Assert.Contains("O-999", body);
            Assert.Contains("42", body);
        }

        // ── Unsupported content type ──────────────────────────────────────────

        [Fact]
        public static void CreateMessage_UnsupportedContentType_ThrowsNotSupportedException()
        {
            var @event = new CloudEvent
            {
                Type            = "test.event",
                Source          = new Uri("https://api.example.com"),
                Id              = Guid.NewGuid().ToString("N"),
                Time            = DateTimeOffset.UtcNow,
                DataContentType = "application/xml",
                Data            = "<root/>",
            };

            Assert.Throws<NotSupportedException>(() => Factory.CreateMessage(@event));
        }

        // ── CorrelationId default ─────────────────────────────────────────────

        [Fact]
        public static void CreateMessage_CorrelationId_IsEmptyByDefault()
        {
            var message = Factory.CreateMessage(MakeJsonEvent());

            Assert.Equal(string.Empty, message.CorrelationId);
        }

        // ── Custom CloudEvent extension attributes ────────────────────────────

        [Fact]
        public static void CreateMessage_CustomCloudEventExtension_IsIncludedInProperties()
        {
            var @event = new CloudEvent
            {
                Type            = "order.placed",
                Source          = new Uri("https://api.example.com"),
                Id              = Guid.NewGuid().ToString("N"),
                Time            = DateTimeOffset.UtcNow,
                DataContentType = "application/json",
                Data            = "{}",
            };

            // Add a custom extension attribute
            @event["customattr"] = "custom-value";

            var message = Factory.CreateMessage(@event);

            Assert.True(message.ApplicationProperties.ContainsKey("customattr"));
            Assert.Equal("custom-value", message.ApplicationProperties["customattr"]);
        }
    }
}
