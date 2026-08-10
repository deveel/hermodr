//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Dapr.Client;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Hermodr
{
    [Trait("Category", "Unit")]
    [Trait("Layer", "Infrastructure")]
    [Trait("Feature", "Dapr")]
    [Trait("Function", "Registration")]
    public static class ServiceInstallationTests
    {
        [Fact]
        public static void UseDapr_RegistersChannel()
        {
            var services = new ServiceCollection();
            services.AddEventPublisher()
                .AddDapr();
            Assert.Contains(services, d =>
                d.ServiceType == typeof(IEventPublishChannel));
        }

        [Fact]
        public static void UseDapr_RegistersDaprClientBuilder()
        {
            var services = new ServiceCollection();
            services.AddEventPublisher()
                .AddDapr();
            Assert.Contains(services, d =>
                d.ServiceType == typeof(IDaprClientBuilder));
        }

        [Fact]
        public static void UseDapr_WithConfigureAction_ConfiguresOptions()
        {
            var services = new ServiceCollection();
            services.AddEventPublisher()
                .AddDapr(options =>
                {
                    options.PubSubName = "my-pubsub";
                    options.Topic = "orders";
                    options.MapAttributesToMetadata = false;
                });

            var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<IOptions<DaprPublishOptions>>().Value;

            Assert.Equal("my-pubsub", options.PubSubName);
            Assert.Equal("orders", options.Topic);
            Assert.False(options.MapAttributesToMetadata);
        }

        [Fact]
        public static void UseDapr_WithSectionPath_BindsOptionsFromConfiguration()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Dapr:PubSubName"] = "cfg-pubsub",
                    ["Dapr:Topic"] = "cfg-topic",
                    ["Dapr:MetadataPrefix"] = "cloudEvent-",
                })
                .Build();

            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddEventPublisher()
                .AddDapr("Dapr");

            var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<IOptions<DaprPublishOptions>>().Value;

            Assert.Equal("cfg-pubsub", options.PubSubName);
            Assert.Equal("cfg-topic", options.Topic);
            Assert.Equal("cloudEvent-", options.MetadataPrefix);
        }

        [Fact]
        public static void UseDapr_DefaultOptions_HasExpectedDefaults()
        {
            var services = new ServiceCollection();
            services.AddEventPublisher()
                .AddDapr();

            var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<IOptions<DaprPublishOptions>>().Value;

            Assert.Equal(string.Empty, options.PubSubName);
            Assert.Null(options.Topic);
            Assert.Null(options.TopicSelector);
            Assert.Null(options.MapAttributesToMetadata);
            Assert.Null(options.MetadataPrefix);
            Assert.Null(options.DaprClientName);
        }
    }
}