// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using Bogus;

using CloudNative.CloudEvents;
using CloudNative.CloudEvents.SystemTextJson;

using Microsoft.Extensions.Options;

using System.Text.Json;

namespace Hermodr
{
    /// <summary>
    /// Unit tests for <see cref="RabbitMqMessageFactory"/> that do not require
    /// a live RabbitMQ broker.
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Layer", "Infrastructure")]
    [Trait("Feature", "RabbitMq")]
    public static class RabbitMqMessageFactoryTests
    {
        // ── Helpers ───────────────────────────────────────────────────────────

        private static readonly Faker Faker = new("en");

        private static RabbitMqMessageFactory CreateFactory(
            RabbitMqMessageContent? content = null,
            RabbitMqMessageFormat? format = null)
        {
            var options = new RabbitMqPublishOptions
            {
                MessageContent = content,
                MessageFormat  = format,
            };
            return new RabbitMqMessageFactory(Options.Create(options));
        }

        private static CloudEvent MakeJsonEvent() => new CloudEvent
        {
            Type            = $"{Faker.Lorem.Word()}.{Faker.Lorem.Word()}",
            Source          = new Uri($"https://{Faker.Internet.DomainName()}"),
            Id              = Faker.Random.Guid().ToString("N"),
            Time            = DateTimeOffset.UtcNow,
            DataContentType = "application/json",
            Data            = JsonSerializer.Serialize(new { OrderId = Faker.Commerce.Ean13(), Total = Faker.Finance.Amount() }),
        };

        // ── CloudEvent / Json (defaults) ──────────────────────────────────────

        [Fact]
        public static void Should_UseCloudEventsJsonContentType_When_DefaultOptionsAreUsed()
        {
            // Arrange
            var factory = CreateFactory();

            // Act
            var message = factory.CreateMessage(MakeJsonEvent());

            // Assert
            Assert.Equal("application/cloudevents+json", message.ContentType);
        }

        [Fact]
        public static void Should_UseUtf8Encoding_When_DefaultOptionsAreUsed()
        {
            // Arrange
            var factory = CreateFactory();

            // Act
            var message = factory.CreateMessage(MakeJsonEvent());

            // Assert
            Assert.Equal("utf-8", message.ContentEncoding);
        }

        [Fact]
        public static void Should_ProduceNonEmptyBody_When_DefaultOptionsAreUsed()
        {
            // Arrange
            var factory = CreateFactory();

            // Act
            var message = factory.CreateMessage(MakeJsonEvent());

            // Assert
            Assert.True(message.Body.Length > 0);
        }

        [Fact]
        public static void Should_ProduceDeserializableCloudEvent_When_CloudEventJsonFormatIsUsed()
        {
            // Arrange
            var factory   = CreateFactory(content: RabbitMqMessageContent.CloudEvent, format: RabbitMqMessageFormat.Json);
            var original  = MakeJsonEvent();

            // Act
            var message   = factory.CreateMessage(original);
            var json      = JsonSerializer.Deserialize<JsonElement>(message.Body.Span);
            var formatter = new JsonEventFormatter();
            var decoded   = formatter.ConvertFromJsonElement(json, null);

            // Assert
            Assert.Equal(original.Id,     decoded.Id);
            Assert.Equal(original.Type,   decoded.Type);
            Assert.Equal(original.Source, decoded.Source);
        }

        // ── EventData / Json ──────────────────────────────────────────────────

        [Fact]
        public static void Should_UseApplicationJsonContentType_When_EventDataJsonFormatIsUsed()
        {
            // Arrange
            var factory = CreateFactory(content: RabbitMqMessageContent.EventData, format: RabbitMqMessageFormat.Json);

            // Act
            var message = factory.CreateMessage(MakeJsonEvent());

            // Assert
            Assert.Equal("application/json", message.ContentType);
        }

        [Fact]
        public static void Should_UseUtf8Encoding_When_EventDataJsonFormatIsUsed()
        {
            // Arrange
            var factory = CreateFactory(content: RabbitMqMessageContent.EventData, format: RabbitMqMessageFormat.Json);

            // Act
            var message = factory.CreateMessage(MakeJsonEvent());

            // Assert
            Assert.Equal("utf-8", message.ContentEncoding);
        }

        [Fact]
        public static void Should_ProduceNonEmptyBody_When_EventDataJsonFormatIsUsed()
        {
            // Arrange
            var factory = CreateFactory(content: RabbitMqMessageContent.EventData, format: RabbitMqMessageFormat.Json);

            // Act
            var message = factory.CreateMessage(MakeJsonEvent());

            // Assert
            Assert.True(message.Body.Length > 0);
        }

        // ── CloudEvent / Binary ───────────────────────────────────────────────

        [Fact]
        public static void Should_ThrowNotSupportedException_When_CloudEventBinaryFormatIsUsed()
        {
            // Arrange
            var factory = CreateFactory(content: RabbitMqMessageContent.CloudEvent, format: RabbitMqMessageFormat.Binary);

            // Act & Assert
            Assert.Throws<NotSupportedException>(() => factory.CreateMessage(MakeJsonEvent()));
        }

        // ── EventData / Binary ────────────────────────────────────────────────

        [Fact]
        public static void Should_ProduceNonEmptyBody_When_EventDataBinaryFormatIsUsed()
        {
            // Arrange
            var factory = CreateFactory(content: RabbitMqMessageContent.EventData, format: RabbitMqMessageFormat.Binary);

            // Act
            var message = factory.CreateMessage(MakeJsonEvent());

            // Assert
            Assert.True(message.Body.Length > 0);
        }

        [Fact]
        public static void Should_UseOctetStreamContentType_When_EventDataBinaryFormatIsUsed()
        {
            // Arrange
            var factory = CreateFactory(content: RabbitMqMessageContent.EventData, format: RabbitMqMessageFormat.Binary);

            // Act
            var message = factory.CreateMessage(MakeJsonEvent());

            // Assert
            Assert.Equal("application/octet-stream", message.ContentType);
        }

        [Fact]
        public static void Should_HaveNullEncoding_When_EventDataBinaryFormatIsUsed()
        {
            // Arrange
            var factory = CreateFactory(content: RabbitMqMessageContent.EventData, format: RabbitMqMessageFormat.Binary);

            // Act
            var message = factory.CreateMessage(MakeJsonEvent());

            // Assert
            Assert.Null(message.ContentEncoding);
        }

        // ── Null event data ───────────────────────────────────────────────────

        [Fact]
        public static void Should_ProduceNullJsonBody_When_CloudEventDataIsNull()
        {
            // Arrange
            var factory = CreateFactory(content: RabbitMqMessageContent.EventData, format: RabbitMqMessageFormat.Json);
            var nullDataEvent = new CloudEvent
            {
                Type   = "order.cancelled",
                Source = new Uri($"https://{Faker.Internet.DomainName()}"),
                Id     = Faker.Random.Guid().ToString("N"),
                Time   = DateTimeOffset.UtcNow,
                // Data is intentionally left null
            };

            // Act
            var message  = factory.CreateMessage(nullDataEvent);
            var bodyText = System.Text.Encoding.UTF8.GetString(message.Body.Span);

            // Assert — JsonSerializer.Serialize(null) produces "null"
            Assert.Equal("null", bodyText);
        }

        // ── ContentType round-trip ────────────────────────────────────────────

        [Fact]
        public static void Should_StartWithCloudEventsContentType_When_CloudEventJsonFormatIsUsed()
        {
            // Arrange
            var factory = CreateFactory(content: RabbitMqMessageContent.CloudEvent, format: RabbitMqMessageFormat.Json);

            // Act
            var message = factory.CreateMessage(MakeJsonEvent());

            // Assert
            Assert.StartsWith("application/cloudevents", message.ContentType);
        }
    }
}

