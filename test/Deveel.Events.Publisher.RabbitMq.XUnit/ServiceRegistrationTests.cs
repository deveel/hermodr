//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
namespace Deveel.Events
{
    [Trait("Function", "Registration")]
    public static class ServiceRegistrationTests
    {
        private const string ValidConnectionString = "amqp://guest:guest@localhost:5672/";
        [Fact]
        public static void UseRabbitMq_WithConfigureAction_RegistersServices()
        {
            var services = new ServiceCollection();
            services.AddEventPublisher()
                .AddRabbitMq(options =>
                {
                    options.ConnectionString = ValidConnectionString;
                    options.ExchangeName = "my-exchange";
                    options.QueueName = "my-queue";
                });
            Assert.Contains(services, d =>
                d.ServiceType == typeof(IEventPublishChannel) &&
                d.ImplementationType == typeof(RabbitMqEventPublishChannel));
            Assert.Contains(services, d =>
                d.ServiceType == typeof(IRabbitMqConnectionFactory) &&
                d.ImplementationType == typeof(RabbitMqConnectionFactory));
            Assert.Contains(services, d =>
                d.ServiceType == typeof(IRabbitMqMessageFactory) &&
                d.ImplementationType == typeof(RabbitMqMessageFactory));
            Assert.Contains(services, d =>
                d.ServiceType == typeof(IConnection));
        }
        [Fact]
        public static void UseRabbitMq_WithConfigureAction_ConfiguresOptions()
        {
            var services = new ServiceCollection();
            services.AddEventPublisher()
                .AddRabbitMq(options =>
                {
                    options.ConnectionString = ValidConnectionString;
                    options.ExchangeName = "my-exchange";
                    options.QueueName = "my-queue";
                    options.RoutingKey = "my-routing-key";
                    options.ClientName = "TestClient";
                    options.PersistentMessages = false;
                    options.PublisherConfirms = false;
                    options.Mandatory = true;
                    options.MessageFormat = RabbitMqMessageFormat.Json;
                    options.MessageContent = RabbitMqMessageContent.CloudEvent;
                    options.ConfirmTimeout = TimeSpan.FromSeconds(10);
                });
            var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<IOptions<RabbitMqEventPublishOptions>>();
            Assert.NotNull(options.Value);
            Assert.Equal(ValidConnectionString, options.Value.ConnectionString);
            Assert.Equal("my-exchange", options.Value.ExchangeName);
            Assert.Equal("my-queue", options.Value.QueueName);
            Assert.Equal("my-routing-key", options.Value.RoutingKey);
            Assert.Equal("TestClient", options.Value.ClientName);
            Assert.False(options.Value.PersistentMessages);
            Assert.False(options.Value.PublisherConfirms);
            Assert.True(options.Value.Mandatory);
            Assert.Equal(RabbitMqMessageFormat.Json, options.Value.MessageFormat);
            Assert.Equal(RabbitMqMessageContent.CloudEvent, options.Value.MessageContent);
            Assert.Equal(TimeSpan.FromSeconds(10), options.Value.ConfirmTimeout);
        }
        [Fact]
        public static void UseRabbitMq_DefaultOptions_HasExpectedDefaults()
        {
            var services = new ServiceCollection();
            services.AddEventPublisher()
                .AddRabbitMq(options =>
                {
                    options.ConnectionString = ValidConnectionString;
                });
            var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<IOptions<RabbitMqEventPublishOptions>>();
            Assert.NotNull(options.Value);
            // Nullable value-type properties are null when not explicitly configured;
            // the effective defaults (true/false/5s etc.) are applied during MergeOptions.
            Assert.Null(options.Value.PersistentMessages);
            Assert.Null(options.Value.PublisherConfirms);
            Assert.Null(options.Value.Mandatory);
            Assert.Null(options.Value.ConfirmTimeout);
            Assert.Null(options.Value.MessageFormat);
            Assert.Null(options.Value.MessageContent);
        }
        [Fact]
        public static void UseRabbitMq_WithSectionPath_BindsOptionsFromConfiguration()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RabbitMq:ConnectionString"] = ValidConnectionString,
                    ["RabbitMq:ExchangeName"] = "config-exchange",
                    ["RabbitMq:QueueName"] = "config-queue",
                    ["RabbitMq:RoutingKey"] = "config-routing-key",
                    ["RabbitMq:ClientName"] = "ConfigClient",
                    ["RabbitMq:PersistentMessages"] = "false",
                    ["RabbitMq:PublisherConfirms"] = "false",
                })
                .Build();
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddEventPublisher()
                .AddRabbitMq("RabbitMq");
            Assert.Contains(services, d =>
                d.ServiceType == typeof(IEventPublishChannel) &&
                d.ImplementationType == typeof(RabbitMqEventPublishChannel));
            var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<IOptions<RabbitMqEventPublishOptions>>();
            Assert.NotNull(options.Value);
            Assert.Equal(ValidConnectionString, options.Value.ConnectionString);
            Assert.Equal("config-exchange", options.Value.ExchangeName);
            Assert.Equal("config-queue", options.Value.QueueName);
            Assert.Equal("config-routing-key", options.Value.RoutingKey);
            Assert.Equal("ConfigClient", options.Value.ClientName);
            Assert.False(options.Value.PersistentMessages);
            Assert.False(options.Value.PublisherConfirms);
        }
        [Fact]
        public static void UseRabbitMq_CustomConnectionFactory_IsNotReplaced()
        {
            var services = new ServiceCollection();
            var customFactory = new CustomRabbitMqConnectionFactory();
            services.AddSingleton<IRabbitMqConnectionFactory>(customFactory);
            services.AddEventPublisher()
                .AddRabbitMq(options =>
                {
                    options.ConnectionString = ValidConnectionString;
                });
            var provider = services.BuildServiceProvider();
            var factory = provider.GetRequiredService<IRabbitMqConnectionFactory>();
            Assert.Same(customFactory, factory);
        }
        private class CustomRabbitMqConnectionFactory : IRabbitMqConnectionFactory
        {
            public Task<IConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
                => throw new NotImplementedException();
        }
    }
}
