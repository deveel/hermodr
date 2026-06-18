// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using CloudNative.CloudEvents;

namespace Hermodr
{
    [Trait("Category", "Unit")]
    [Trait("Layer", "Application")]
    [Trait("Feature", "VersionAwareRouting")]
    public static class VersionAwareFilterTests
    {
        private static CloudEvent MakeEvent(string type, string? dataVersion = null, string? dataSchemaVersion = null)
        {
            Uri? dataSchema = null;
            if (dataSchemaVersion != null)
                dataSchema = new Uri($"https://schemas.example.com/events/{type}/{dataSchemaVersion}");

            var cloudEvent = new CloudEvent
            {
                Type = type,
                Source = new Uri("https://test.example.com/source"),
                Id = Guid.NewGuid().ToString("N"),
                DataContentType = "application/json",
                Data = """{"name": "John"}""",
                DataSchema = dataSchema
            };

            if (dataVersion != null)
            {
                var attr = CloudEventAttribute.CreateExtension("dataversion", CloudEventAttributeType.String);
                cloudEvent[attr] = dataVersion;
            }

            return cloudEvent;
        }

        [Fact]
        public static void BySchemaVersion_Matches_DataVersion()
        {
            // Arrange
            var filter = EventFilter.BySchemaVersion("2.0");
            var @event = MakeEvent("test.event", dataVersion: "2.0");

            // Act & Assert
            Assert.True(filter.Matches(@event, EventSubscriptionContext.Empty));
        }

        [Fact]
        public static void BySchemaVersion_Matches_DataSchemaVersion_When_DataVersionMissing()
        {
            // Arrange
            var filter = EventFilter.BySchemaVersion("2.0");
            var @event = MakeEvent("test.event", dataSchemaVersion: "2.0");

            // Act & Assert
            Assert.True(filter.Matches(@event, EventSubscriptionContext.Empty));
        }

        [Fact]
        public static void BySchemaVersion_DoesNotMatch_When_VersionDiffers()
        {
            // Arrange
            var filter = EventFilter.BySchemaVersion("2.0");
            var @event = MakeEvent("test.event", dataVersion: "1.0");

            // Act & Assert
            Assert.False(filter.Matches(@event, EventSubscriptionContext.Empty));
        }

        [Fact]
        public static void BySchemaVersionRange_Matches_VersionInsideRange()
        {
            // Arrange
            var filter = EventFilter.BySchemaVersionRange("1.0", "2.0");
            var @event = MakeEvent("test.event", dataVersion: "1.5");

            // Act & Assert
            Assert.True(filter.Matches(@event, EventSubscriptionContext.Empty));
        }

        [Fact]
        public static void BySchemaVersionRange_DoesNotMatch_VersionOutsideRange()
        {
            // Arrange
            var filter = EventFilter.BySchemaVersionRange("1.0", "2.0");
            var @event = MakeEvent("test.event", dataVersion: "3.0");

            // Act & Assert
            Assert.False(filter.Matches(@event, EventSubscriptionContext.Empty));
        }

        [Fact]
        public static void BySchemaVersionRange_Handles_MinVersionOnly()
        {
            // Arrange
            var filter = EventFilter.BySchemaVersionRange("2.0", null);
            var @event = MakeEvent("test.event", dataVersion: "2.1");

            // Act & Assert
            Assert.True(filter.Matches(@event, EventSubscriptionContext.Empty));
        }

        [Fact]
        public static void BySchemaVersionRange_Handles_MaxVersionOnly()
        {
            // Arrange
            var filter = EventFilter.BySchemaVersionRange(null, "2.0");
            var @event = MakeEvent("test.event", dataVersion: "1.5");

            // Act & Assert
            Assert.True(filter.Matches(@event, EventSubscriptionContext.Empty));
        }

        [Fact]
        public static void BySchemaVersionRange_Handles_TwoDigitVersionComparison()
        {
            // Arrange
            var filter = EventFilter.BySchemaVersionRange("1.0", "1.10");
            var @event = MakeEvent("test.event", dataVersion: "1.2");

            // Act & Assert
            Assert.True(filter.Matches(@event, EventSubscriptionContext.Empty));
        }

        [Fact]
        public static void EventFilterBuilder_BySchemaVersion_AddsCondition()
        {
            // Arrange
            var filter = EventFilter.New()
                .ByType("test.event")
                .BySchemaVersion("2.0")
                .Build();

            var matching = MakeEvent("test.event", dataVersion: "2.0");
            var nonMatching = MakeEvent("test.event", dataVersion: "1.0");

            // Act & Assert
            Assert.True(filter.Matches(matching, EventSubscriptionContext.Empty));
            Assert.False(filter.Matches(nonMatching, EventSubscriptionContext.Empty));
        }

        [Fact]
        public static void EventFilterBuilder_BySchemaVersionRange_AddsCondition()
        {
            // Arrange
            var filter = EventFilter.New()
                .ByType("test.event")
                .BySchemaVersionRange("1.0", "2.0")
                .Build();

            var matching = MakeEvent("test.event", dataVersion: "1.5");
            var nonMatching = MakeEvent("test.event", dataVersion: "3.0");

            // Act & Assert
            Assert.True(filter.Matches(matching, EventSubscriptionContext.Empty));
            Assert.False(filter.Matches(nonMatching, EventSubscriptionContext.Empty));
        }
    }
}
