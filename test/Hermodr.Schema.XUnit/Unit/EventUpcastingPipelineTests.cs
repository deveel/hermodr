// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using System.Text.Json.Nodes;

namespace Hermodr
{
    [Trait("Category", "Unit")]
    [Trait("Layer", "Domain")]
    [Trait("Feature", "EventUpcasting")]
    public static class EventUpcastingPipelineTests
    {
        [Fact]
        public static async Task Should_Upcast_Directly_When_SingleUpcasterRegistered()
        {
            // Arrange
            var registry = new EventUpcasterRegistry();
            registry.Register(new RenamePropertyUpcaster("test.event", new Version(1, 0), new Version(2, 0), "oldName", "newName"));

            var pipeline = new EventUpcastingPipeline(registry);
            var data = JsonNode.Parse("""{"oldName": "John"}""")!;

            // Act
            var result = await pipeline.UpcastAsync("test.event", data, new Version(1, 0), new Version(2, 0));

            // Assert
            Assert.Equal("John", result["newName"]?.GetValue<string>());
            Assert.Null(result["oldName"]);
        }

        [Fact]
        public static async Task Should_ChainUpcasters_When_IntermediateStepIsRequired()
        {
            // Arrange
            var registry = new EventUpcasterRegistry();
            registry.Register(new RenamePropertyUpcaster("test.event", new Version(1, 0), new Version(2, 0), "name", "fullName"));
            registry.Register(new RenamePropertyUpcaster("test.event", new Version(2, 0), new Version(3, 0), "fullName", "displayName"));

            var schemaRegistry = new EventSchemaRegistry(new EventSchemaFactory());
            schemaRegistry.Register(CreateSchema("test.event", "1.0"));
            schemaRegistry.Register(CreateSchema("test.event", "2.0"));
            schemaRegistry.Register(CreateSchema("test.event", "3.0"));

            var pipeline = new EventUpcastingPipeline(registry, schemaRegistry);
            var data = JsonNode.Parse("""{"name": "John"}""")!;

            // Act
            var result = await pipeline.UpcastAsync("test.event", data, new Version(1, 0), new Version(3, 0));

            // Assert
            Assert.Equal("John", result["displayName"]?.GetValue<string>());
            Assert.Null(result["name"]);
            Assert.Null(result["fullName"]);
        }

        [Fact]
        public static async Task Should_Upcast_Directly_When_DirectPathExists_EvenWithIntermediateSchemas()
        {
            // Arrange
            var registry = new EventUpcasterRegistry();
            registry.Register(new RenamePropertyUpcaster("test.event", new Version(1, 0), new Version(3, 0), "name", "displayName"));

            var schemaRegistry = new EventSchemaRegistry(new EventSchemaFactory());
            schemaRegistry.Register(CreateSchema("test.event", "1.0"));
            schemaRegistry.Register(CreateSchema("test.event", "2.0"));
            schemaRegistry.Register(CreateSchema("test.event", "3.0"));

            var pipeline = new EventUpcastingPipeline(registry, schemaRegistry);
            var data = JsonNode.Parse("""{"name": "John"}""")!;

            // Act
            var result = await pipeline.UpcastAsync("test.event", data, new Version(1, 0), new Version(3, 0));

            // Assert
            Assert.Equal("John", result["displayName"]?.GetValue<string>());
            Assert.Null(result["name"]);
        }

        [Fact]
        public static async Task Should_Throw_When_NoUpcasterChainExists()
        {
            // Arrange
            var registry = new EventUpcasterRegistry();
            var pipeline = new EventUpcastingPipeline(registry);
            var data = JsonNode.Parse("""{"name": "John"}""")!;

            // Act & Assert
            await Assert.ThrowsAsync<EventUpcastingException>(() =>
                pipeline.UpcastAsync("test.event", data, new Version(1, 0), new Version(2, 0)));
        }

        [Fact]
        public static async Task Should_IgnoreUpcasters_ForOtherEventTypes()
        {
            // Arrange
            var registry = new EventUpcasterRegistry();
            registry.Register(new RenamePropertyUpcaster("other.event", new Version(1, 0), new Version(2, 0), "a", "b"));

            var pipeline = new EventUpcastingPipeline(registry);
            var data = JsonNode.Parse("""{"name": "John"}""")!;

            // Act & Assert
            await Assert.ThrowsAsync<EventUpcastingException>(() =>
                pipeline.UpcastAsync("test.event", data, new Version(1, 0), new Version(2, 0)));
        }

        [Fact]
        public static async Task Should_ReturnDataUnchanged_When_SourceEqualsTargetVersion()
        {
            // Arrange
            var registry = new EventUpcasterRegistry();
            var pipeline = new EventUpcastingPipeline(registry);
            var data = JsonNode.Parse("""{"name": "John"}""")!;

            // Act
            var result = await pipeline.UpcastAsync("test.event", data, new Version(2, 0), new Version(2, 0));

            // Assert
            Assert.Equal("John", result["name"]?.GetValue<string>());
        }

        private static EventSchema CreateSchema(string eventType, string version)
        {
            return EventSchema.Build(eventType)
                .WithVersion(version)
                .WithContentType("application/json")
                .Build();
        }

        private sealed class RenamePropertyUpcaster : IEventUpcaster
        {
            private readonly string _eventType;
            private readonly Version _fromVersion;
            private readonly Version _toVersion;
            private readonly string _oldName;
            private readonly string _newName;

            public RenamePropertyUpcaster(
                string eventType,
                Version fromVersion,
                Version toVersion,
                string oldName,
                string newName)
            {
                _eventType = eventType;
                _fromVersion = fromVersion;
                _toVersion = toVersion;
                _oldName = oldName;
                _newName = newName;
            }

            public bool CanUpcast(string eventType, Version fromVersion, Version toVersion)
                => eventType == _eventType && fromVersion == _fromVersion && toVersion == _toVersion;

            public Task<JsonNode> UpcastAsync(UpcastContext context, CancellationToken cancellationToken = default)
            {
                if (context.Data is JsonObject obj && obj[_oldName] is { } value)
                {
                    obj[_newName] = value.DeepClone();
                    obj.Remove(_oldName);
                }

                return Task.FromResult(context.Data);
            }
        }
    }
}
