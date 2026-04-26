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
    }
}


