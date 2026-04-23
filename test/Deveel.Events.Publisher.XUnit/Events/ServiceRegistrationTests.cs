//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

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

        private class CustomEventPublisher : EventPublisher
        {
            public CustomEventPublisher(
                IOptions<EventPublisherOptions> options,
                IEnumerable<IEventPublishChannel> channels,
                IEventCreator? eventCreator = null,
                IEventIdGenerator? idGenerator = null,
                IEventSystemTime? systemTime = null)
                : base(options, channels, eventCreator, idGenerator, systemTime)
            {
            }
        }
    }
}


