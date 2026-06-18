//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Text.Json.Nodes;

using CloudNative.CloudEvents;
using Microsoft.Extensions.DependencyInjection;

namespace Hermodr
{
    [Trait("Function", "EventUpcasting")]
    public class EventUpcastingMiddlewareTests
    {
        private static CloudEvent MakeEvent(
            string type = "test.event",
            string? version = null,
            string? jsonData = null)
        {
            var dataVersionAttr = CloudEventAttribute.CreateExtension("dataversion", CloudEventAttributeType.String);

            var cloudEvent = new CloudEvent
            {
                Type = type,
                Source = new Uri("https://test.example.com/source"),
                Id = Guid.NewGuid().ToString("N"),
                Time = DateTimeOffset.UtcNow,
                DataContentType = "application/json",
                Data = jsonData ?? """{"name": "John"}""",
                DataSchema = new Uri($"https://schemas.example.com/events/{type}/{version ?? "1.0"}")
            };

            if (version != null)
                cloudEvent[dataVersionAttr] = version;

            return cloudEvent;
        }

        private static (IServiceProvider Provider, List<CloudEvent> Received) BuildProvider(
            Action<EventPublisherBuilder>? configure = null,
            Action<EventSchemaServicesOptions>? configureSchema = null)
        {
            var received = new List<CloudEvent>();
            var services = new ServiceCollection().AddLogging();
            var builder = services.AddEventPublisher(opts =>
                opts.Source = new Uri("https://test.example.com/source"));

            services.AddEventSchemaServices(schemaOptions =>
            {
                schemaOptions.SchemaRegistrations.Add(registry =>
                {
                    var schemaV1 = new EventSchema("test.event", "1.0", "application/json");
                    schemaV1.Properties.Add(new EventProperty("name", "string", "1.0"));
                    registry.Register(schemaV1);

                    var schemaV2 = new EventSchema("test.event", "2.0", "application/json");
                    schemaV2.Properties.Add(new EventProperty("name", "string", "2.0"));
                    schemaV2.Properties.Add(new EventProperty("email", "string", "2.0"));
                    registry.Register(schemaV2);
                });

                configureSchema?.Invoke(schemaOptions);
            });

            configure?.Invoke(builder);
            builder.AddTestChannel(e => received.Add(e));
            return (services.BuildServiceProvider(), received);
        }

        [Fact]
        public async Task Middleware_UpcastsEvent_ToLatestSchemaVersion()
        {
            // Arrange
            var (provider, received) = BuildProvider(builder =>
            {
                builder.UseEventUpcasting();
                builder.Services.AddSingleton<IEventUpcaster>(new AddEmailUpcaster());
            });

            var publisher = provider.GetRequiredService<IEventPublisher>();
            var cloudEvent = MakeEvent("test.event", "1.0", """{"name": "John"}""");

            // Act
            await publisher.PublishEventAsync(cloudEvent);

            // Assert
            Assert.Single(received);
            Assert.Equal("2.0", received[0]["dataversion"]);
            Assert.EndsWith("/test.event/2.0", received[0].DataSchema!.ToString());

            var receivedData = JsonNode.Parse((string)received[0].Data!);
            Assert.Equal("john@example.com", receivedData!["email"]!.GetValue<string>());
        }

        [Fact]
        public async Task Middleware_Skips_When_EventAlreadyAtLatestVersion()
        {
            // Arrange
            var (provider, received) = BuildProvider(builder =>
            {
                builder.UseEventUpcasting();
                builder.Services.AddSingleton<IEventUpcaster>(new AddEmailUpcaster());
            });

            var publisher = provider.GetRequiredService<IEventPublisher>();
            var cloudEvent = MakeEvent("test.event", "2.0", """{"name": "John", "email": "john@example.com"}""");

            // Act
            await publisher.PublishEventAsync(cloudEvent);

            // Assert
            Assert.Single(received);
            Assert.Equal("2.0", received[0]["dataversion"]);
        }

        [Fact]
        public async Task Middleware_PassThrough_When_NoUpcasterRegistered()
        {
            // Arrange
            var (provider, received) = BuildProvider(builder =>
                builder.UseEventUpcasting());

            var publisher = provider.GetRequiredService<IEventPublisher>();
            var cloudEvent = MakeEvent("test.event", "1.0");

            // Act
            await publisher.PublishEventAsync(cloudEvent);

            // Assert
            Assert.Single(received);
            Assert.Equal("1.0", received[0]["dataversion"]);
        }

        [Fact]
        public async Task Middleware_Throws_When_MissingUpcasterBehaviorIsThrow()
        {
            // Arrange
            var (provider, _) = BuildProvider(builder =>
                builder.UseEventUpcasting(opts =>
                    opts.MissingUpcasterBehavior = MissingUpcasterBehavior.Throw));

            var publisher = provider.GetRequiredService<IEventPublisher>();
            var cloudEvent = MakeEvent("test.event", "1.0");

            // Act & Assert
            await Assert.ThrowsAsync<EventUpcastingException>(() =>
                publisher.PublishEventAsync(cloudEvent));
        }

        [Fact]
        public async Task Middleware_Skips_When_NoSchemaRegistered()
        {
            // Arrange
            var (provider, received) = BuildProvider(builder =>
                builder.UseEventUpcasting());

            var publisher = provider.GetRequiredService<IEventPublisher>();
            var cloudEvent = MakeEvent("unknown.event", "1.0");

            // Act
            await publisher.PublishEventAsync(cloudEvent);

            // Assert
            Assert.Single(received);
        }

        [Fact]
        public async Task Middleware_Skips_When_NoDataVersionPresent()
        {
            // Arrange
            var (provider, received) = BuildProvider(builder =>
                builder.UseEventUpcasting());

            var publisher = provider.GetRequiredService<IEventPublisher>();
            var cloudEvent = new CloudEvent
            {
                Type = "test.event",
                Source = new Uri("https://test.example.com/source"),
                Id = Guid.NewGuid().ToString("N"),
                DataContentType = "application/json",
                Data = """{"name": "John"}"""
            };

            // Act
            await publisher.PublishEventAsync(cloudEvent);

            // Assert
            Assert.Single(received);
        }

        private sealed class AddEmailUpcaster : IEventUpcaster
        {
            public bool CanUpcast(string eventType, Version fromVersion, Version toVersion)
                => eventType == "test.event" && fromVersion == new Version(1, 0) && toVersion == new Version(2, 0);

            public Task<JsonNode> UpcastAsync(UpcastContext context, CancellationToken cancellationToken = default)
            {
                if (context.Data is JsonObject obj)
                    obj["email"] = "john@example.com";

                return Task.FromResult(context.Data);
            }
        }
    }
}
