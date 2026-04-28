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
    [Trait("Function", "Registration")]
    public static class ServiceRegistrationTests
    {
        [Fact]
        public static void AddEventPublisher_RegistersDefaultServices()
        {
            var services = new ServiceCollection();
            services.AddEventPublisher();

            var provider = services.BuildServiceProvider();

            Assert.NotNull(provider.GetService<EventPublisher>());
            Assert.NotNull(provider.GetService<IEventCreator>());
            Assert.NotNull(provider.GetService<IEventIdGenerator>());
            Assert.NotNull(provider.GetService<IEventSystemTime>());
        }

        [Fact]
        public static void AddEventPublisher_WithConfigureAction_ConfiguresOptions()
        {
            var services = new ServiceCollection();
            services.AddEventPublisher(options =>
            {
                options.Source = new Uri("https://api.example.com/my-service");
                options.ThrowOnErrors = true;
                options.DataSchemaBaseUri = new Uri("https://schemas.example.com/events");
                options.Attributes["env"] = "test";
            });

            var provider = services.BuildServiceProvider();

            Assert.NotNull(provider.GetService<EventPublisher>());

            var options = provider.GetRequiredService<IOptions<EventPublisherOptions>>();
            Assert.NotNull(options.Value);
            Assert.Equal(new Uri("https://api.example.com/my-service"), options.Value.Source);
            Assert.True(options.Value.ThrowOnErrors);
            Assert.Equal(new Uri("https://schemas.example.com/events"), options.Value.DataSchemaBaseUri);
            Assert.Contains("env", options.Value.Attributes.Keys);
            Assert.Equal("test", options.Value.Attributes["env"]);
        }

        [Fact]
        public static void AddEventPublisher_WithSectionPath_BindsOptionsFromConfiguration()
        {
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

            var provider = services.BuildServiceProvider();

            Assert.NotNull(provider.GetService<EventPublisher>());

            var options = provider.GetRequiredService<IOptions<EventPublisherOptions>>();
            Assert.NotNull(options.Value);
            Assert.Equal(new Uri("https://api.example.com/my-service"), options.Value.Source);
            Assert.True(options.Value.ThrowOnErrors);
            Assert.Equal(new Uri("https://schemas.example.com/events"), options.Value.DataSchemaBaseUri);
        }

        [Fact]
        public static void UseGuid_ReplacesDefaultIdGenerator()
        {
            var services = new ServiceCollection();
            services.AddEventPublisher()
                .UseGuid("N");

            var provider = services.BuildServiceProvider();

            var idGenerator = provider.GetRequiredService<IEventIdGenerator>();
            Assert.IsType<EventGuidGenerator>(idGenerator);

            var options = provider.GetRequiredService<IOptions<EventGuidGeneratorOptions>>();
            Assert.Equal("N", options.Value.Format);
        }

        [Fact]
        public static void UsePublisher_ReplacesDefaultPublisher()
        {
            var services = new ServiceCollection();
            services.AddEventPublisher()
                .UsePublisher<CustomEventPublisher>();

            var provider = services.BuildServiceProvider();

            Assert.NotNull(provider.GetService<EventPublisher>());
            Assert.IsType<CustomEventPublisher>(provider.GetService<EventPublisher>());
        }

        [Fact]
        public static void UseSystemTime_ReplacesDefaultSystemTime()
        {
            var services = new ServiceCollection();
            services.AddEventPublisher()
                .UseSystemTime<CustomSystemTime>();

            var provider = services.BuildServiceProvider();

            var systemTime = provider.GetRequiredService<IEventSystemTime>();
            Assert.IsType<CustomSystemTime>(systemTime);
        }

        [Fact]
        public static void AddEventPublisher_WithConfigureAction_NoAttributes_DefaultsToEmpty()
        {
            var services = new ServiceCollection();
            services.AddEventPublisher(options =>
            {
                options.Source = new Uri("https://api.example.com/svc");
            });

            var provider = services.BuildServiceProvider();

            var options = provider.GetRequiredService<IOptions<EventPublisherOptions>>();
            Assert.Empty(options.Value.Attributes);
        }

        [Fact]
        public static void AddEventPublisher_WithVersionedRegisteredEventType_WithoutDataSchemaBaseUri_FailsValidation()
        {
            var services = new ServiceCollection();
            services.AddSingleton<VersionedRegisteredEventData>();
            services.AddEventPublisher();

            var provider = services.BuildServiceProvider();

            var ex = Assert.Throws<OptionsValidationException>(() =>
                _ = provider.GetRequiredService<IOptions<EventPublisherOptions>>().Value);

            Assert.Contains("EventPublisherOptions.DataSchemaBaseUri", ex.Message);
            Assert.Contains(nameof(VersionedRegisteredEventData), ex.Message);
        }

        [Fact]
        public static void AddEventPublisher_WithVersionedRegisteredEventType_AndDataSchemaBaseUri_PassesValidation()
        {
            var services = new ServiceCollection();
            services.AddSingleton<VersionedRegisteredEventData>();
            services.AddEventPublisher(options =>
            {
                options.DataSchemaBaseUri = new Uri("https://schemas.example.com/events");
            });

            var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<IOptions<EventPublisherOptions>>();

            Assert.NotNull(options.Value);
            Assert.Equal(new Uri("https://schemas.example.com/events"), options.Value.DataSchemaBaseUri);
        }

        private class CustomEventPublisher : EventPublisher
        {
            public CustomEventPublisher(
                IOptions<EventPublisherOptions> options,
                IEnumerable<IEventPublishChannel> channels,
                IServiceProvider serviceProvider)
                : base(options, channels, serviceProvider)
            {
            }
        }

        private class CustomSystemTime : IEventSystemTime
        {
            public DateTimeOffset UtcNow => new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        }

        [Event("publisher.registration.versioned", "1.0")]
        private class VersionedRegisteredEventData
        {
        }
    }
}


