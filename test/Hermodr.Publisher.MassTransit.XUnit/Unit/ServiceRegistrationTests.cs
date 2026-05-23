//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
namespace Hermodr
{
    [Trait("Category", "Unit")]
    [Trait("Layer", "Infrastructure")]
    [Trait("Feature", "MassTransit")]
    [Trait("Function", "Registration")]
    public static class ServiceRegistrationTests
    {
        [Fact]
        public static void UseMassTransit_RegistersChannel()
        {
            var services = new ServiceCollection();
            services.AddEventPublisher()
                .AddMassTransit();
            Assert.Contains(services, d =>
                d.ServiceType == typeof(IEventPublishChannel));
        }
        [Fact]
        public static void UseMassTransit_WithConfigureAction_ConfiguresOptions()
        {
            var services = new ServiceCollection();
            services.AddEventPublisher()
                .AddMassTransit(options =>
                {
                    options.DestinationAddress = new Uri("rabbitmq://localhost/my-queue");
                    options.MapAttributesToHeaders = false;
                });
            Assert.Contains(services, d =>
                d.ServiceType == typeof(IEventPublishChannel));
            var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<IOptions<MassTransitPublishOptions>>();
            Assert.NotNull(options.Value);
            Assert.Equal(new Uri("rabbitmq://localhost/my-queue"), options.Value.DestinationAddress);
            Assert.False(options.Value.MapAttributesToHeaders);
        }
        [Fact]
        public static void UseMassTransit_WithSectionPath_BindsOptionsFromConfiguration()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["MassTransit:DestinationAddress"] = "rabbitmq://localhost/my-queue",
                    ["MassTransit:MapAttributesToHeaders"] = "false",
                })
                .Build();
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddEventPublisher()
                .AddMassTransit("MassTransit");
            Assert.Contains(services, d =>
                d.ServiceType == typeof(IEventPublishChannel));
            var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<IOptions<MassTransitPublishOptions>>();
            Assert.NotNull(options.Value);
            Assert.Equal(new Uri("rabbitmq://localhost/my-queue"), options.Value.DestinationAddress);
            Assert.False(options.Value.MapAttributesToHeaders);
        }
        [Fact]
        public static void UseMassTransit_DefaultOptions_HasExpectedDefaults()
        {
            var services = new ServiceCollection();
            services.AddEventPublisher()
                .AddMassTransit();
            var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<IOptions<MassTransitPublishOptions>>();
            Assert.NotNull(options.Value);
            Assert.Null(options.Value.DestinationAddress);
            // Nullable: null means "use the effective default (true)" during merge.
            Assert.Null(options.Value.MapAttributesToHeaders);
        }
    }
}
