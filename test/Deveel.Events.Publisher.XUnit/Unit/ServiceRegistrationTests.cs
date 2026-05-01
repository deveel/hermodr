//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Deveel.Events
{
    [Trait("Category", "Unit")]
    [Trait("Layer", "Application")]
    [Trait("Feature", "EventPublisher")]
    public static class ServiceRegistrationTests
    {
        [Fact]
        public static void Should_RegisterDefaultServices_When_AddEventPublisherIsCalled()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddEventPublisher();

            // Act
            var provider = services.BuildServiceProvider();

            // Assert
            Assert.NotNull(provider.GetService<EventPublisher>());
            Assert.NotNull(provider.GetService<IEventFactory>());
            Assert.NotNull(provider.GetService<IEventIdGenerator>());
            Assert.NotNull(provider.GetService<IEventSystemTime>());
        }

        [Fact]
        public static void Should_ConfigureOptions_When_ConfigureActionIsProvided()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddEventPublisher(options =>
            {
                options.Source = new Uri("https://api.example.com/my-service");
                options.ThrowOnErrors = true;
                options.DataSchemaBaseUri = new Uri("https://schemas.example.com/events");
                options.Attributes["env"] = "test";
            });

            // Act
            var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<IOptions<EventPublisherOptions>>();

            // Assert
            Assert.NotNull(provider.GetService<EventPublisher>());
            Assert.NotNull(options.Value);
            Assert.Equal(new Uri("https://api.example.com/my-service"), options.Value.Source);
            Assert.True(options.Value.ThrowOnErrors);
            Assert.Equal(new Uri("https://schemas.example.com/events"), options.Value.DataSchemaBaseUri);
            Assert.Contains("env", options.Value.Attributes.Keys);
            Assert.Equal("test", options.Value.Attributes["env"]);
        }

        [Fact]
        public static void Should_BindOptionsFromConfiguration_When_SectionPathIsProvided()
        {
            // Arrange
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Events:Source"] = "https://api.example.com/my-service",
                    ["Events:ThrowOnErrors"] = "true",
                    ["Events:DataSchemaBaseUri"] = "https://schemas.example.com/events",
                })
                .Build();

            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddEventPublisher("Events");

            // Act
            var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<IOptions<EventPublisherOptions>>();

            // Assert
            Assert.NotNull(provider.GetService<EventPublisher>());
            Assert.NotNull(options.Value);
            Assert.Equal(new Uri("https://api.example.com/my-service"), options.Value.Source);
            Assert.True(options.Value.ThrowOnErrors);
            Assert.Equal(new Uri("https://schemas.example.com/events"), options.Value.DataSchemaBaseUri);
        }

        [Fact]
        public static void Should_ReplaceDefaultIdGenerator_When_UseGuidIsCalled()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddEventPublisher()
                .UseGuid("N");

            // Act
            var provider = services.BuildServiceProvider();
            var idGenerator = provider.GetRequiredService<IEventIdGenerator>();
            var options = provider.GetRequiredService<IOptions<EventGuidGeneratorOptions>>();

            // Assert
            Assert.IsType<EventGuidGenerator>(idGenerator);
            Assert.Equal("N", options.Value.Format);
        }

        [Fact]
        public static void Should_ReplaceDefaultPublisher_When_UsePublisherIsCalled()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddEventPublisher()
                .UsePublisher<CustomEventPublisher>();

            // Act
            var provider = services.BuildServiceProvider();

            // Assert
            Assert.NotNull(provider.GetService<EventPublisher>());
            Assert.IsType<CustomEventPublisher>(provider.GetService<EventPublisher>());
        }

        [Fact]
        public static void Should_ReplaceDefaultSystemTime_When_UseSystemTimeIsCalled()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddEventPublisher()
                .UseSystemTime<CustomSystemTime>();

            // Act
            var provider = services.BuildServiceProvider();
            var systemTime = provider.GetRequiredService<IEventSystemTime>();

            // Assert
            Assert.IsType<CustomSystemTime>(systemTime);
        }

        [Fact]
        public static void Should_DefaultToEmptyAttributes_When_NoAttributesAreConfigured()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddEventPublisher(options =>
            {
                options.Source = new Uri("https://api.example.com/svc");
            });

            // Act
            var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<IOptions<EventPublisherOptions>>();

            // Assert
            Assert.Empty(options.Value.Attributes);
        }

        [Fact]
        public static void Should_FailValidation_When_VersionedEventTypeHasNoDataSchemaBaseUri()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddSingleton<VersionedRegisteredEventData>();
            services.AddEventPublisher();
            var provider = services.BuildServiceProvider();

            // Act & Assert
            var ex = Assert.Throws<OptionsValidationException>(() =>
                _ = provider.GetRequiredService<IOptions<EventPublisherOptions>>().Value);

            Assert.Contains("EventPublisherOptions.DataSchemaBaseUri", ex.Message);
            Assert.Contains(nameof(VersionedRegisteredEventData), ex.Message);
        }

        [Fact]
        public static void Should_PassValidation_When_VersionedEventTypeHasDataSchemaBaseUri()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddSingleton<VersionedRegisteredEventData>();
            services.AddEventPublisher(options =>
            {
                options.DataSchemaBaseUri = new Uri("https://schemas.example.com/events");
            });

            // Act
            var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<IOptions<EventPublisherOptions>>();

            // Assert
            Assert.NotNull(options.Value);
            Assert.Equal(new Uri("https://schemas.example.com/events"), options.Value.DataSchemaBaseUri);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private class CustomEventPublisher : EventPublisher
        {
            public CustomEventPublisher(
                IOptions<EventPublisherOptions> options,
                IEnumerable<IEventPublishChannel> channels,
                IServiceProvider serviceProvider)
                : base(options, channels, serviceProvider) { }
        }

        private class CustomSystemTime : IEventSystemTime
        {
            public DateTimeOffset UtcNow => new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        }

        [Event("publisher.registration.versioned", "1.0")]
        private class VersionedRegisteredEventData { }
    }
}

