//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Hermodr
{
    [Trait("Category", "Unit")]
    [Trait("Layer", "Infrastructure")]
    [Trait("Feature", "RabbitMq")]
    public static class ServiceRegistrationTests
    {
        private const string ValidConnectionString = "amqp://guest:guest@localhost:5672/";

        [Fact]
        public static void Should_RegisterRabbitMqServices_When_AddRabbitMqWithConfigureActionIsCalled()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddEventPublisher()
                .AddRabbitMq(options =>
                {
                    options.ConnectionString = ValidConnectionString;
                    options.ExchangeName     = "my-exchange";
                    options.QueueName        = "my-queue";
                });

            // Act & Assert
            Assert.Contains(services, d =>
                d.ServiceType == typeof(IEventPublishChannel));
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
        public static void Should_ConfigureOptions_When_AddRabbitMqWithConfigureActionIsCalled()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddEventPublisher()
                .AddRabbitMq(options =>
                {
                    options.ConnectionString  = ValidConnectionString;
                    options.ExchangeName      = "my-exchange";
                    options.QueueName         = "my-queue";
                    options.RoutingKey        = "my-routing-key";
                    options.ClientName        = "TestClient";
                    options.PersistentMessages = false;
                    options.PublisherConfirms  = false;
                    options.Mandatory          = true;
                    options.MessageFormat      = RabbitMqMessageFormat.Json;
                    options.MessageContent     = RabbitMqMessageContent.CloudEvent;
                    options.ConfirmTimeout     = TimeSpan.FromSeconds(10);
                });

            // Act
            var provider = services.BuildServiceProvider();
            var options  = provider.GetRequiredService<IOptions<RabbitMqPublishOptions>>();

            // Assert
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
        public static void Should_HaveNullableDefaults_When_AddRabbitMqIsCalledWithMinimalOptions()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddEventPublisher()
                .AddRabbitMq(options =>
                {
                    options.ConnectionString = ValidConnectionString;
                });

            // Act
            var provider = services.BuildServiceProvider();
            var options  = provider.GetRequiredService<IOptions<RabbitMqPublishOptions>>();

            // Assert — nullable value-type properties are null when not explicitly configured
            Assert.NotNull(options.Value);
            Assert.Null(options.Value.PersistentMessages);
            Assert.Null(options.Value.PublisherConfirms);
            Assert.Null(options.Value.Mandatory);
            Assert.Null(options.Value.ConfirmTimeout);
            Assert.Null(options.Value.MessageFormat);
            Assert.Null(options.Value.MessageContent);
        }

        [Fact]
        public static void Should_BindOptionsFromConfiguration_When_SectionPathIsProvided()
        {
            // Arrange
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RabbitMq:ConnectionString"] = ValidConnectionString,
                    ["RabbitMq:ExchangeName"]     = "config-exchange",
                    ["RabbitMq:QueueName"]        = "config-queue",
                    ["RabbitMq:RoutingKey"]        = "config-routing-key",
                    ["RabbitMq:ClientName"]        = "ConfigClient",
                    ["RabbitMq:PersistentMessages"] = "false",
                    ["RabbitMq:PublisherConfirms"]  = "false",
                })
                .Build();

            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddEventPublisher()
                .AddRabbitMq("RabbitMq");

            // Act
            var provider = services.BuildServiceProvider();
            var options  = provider.GetRequiredService<IOptions<RabbitMqPublishOptions>>();

            // Assert
            Assert.Contains(services, d => d.ServiceType == typeof(IEventPublishChannel));
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
        public static void Should_PreserveCustomConnectionFactory_When_AlreadyRegistered()
        {
            // Arrange
            var customFactory = new CustomRabbitMqConnectionFactory();
            var services = new ServiceCollection();
            services.AddSingleton<IRabbitMqConnectionFactory>(customFactory);
            services.AddEventPublisher()
                .AddRabbitMq(options =>
                {
                    options.ConnectionString = ValidConnectionString;
                });

            // Act
            var provider = services.BuildServiceProvider();
            var factory  = provider.GetRequiredService<IRabbitMqConnectionFactory>();

            // Assert
            Assert.Same(customFactory, factory);
        }

        [Fact]
        public static void Should_BindTypedOptionsFromConfiguration_When_TypedSectionPathIsProvided()
        {
            // Arrange
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Orders:ExchangeName"] = "orders-exchange",
                    ["Orders:RoutingKey"]   = "order.created",
                })
                .Build();

            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddEventPublisher()
                .AddRabbitMq(options => { options.ConnectionString = ValidConnectionString; })
                .AddRabbitMq<OrderEvent>("Orders");

            // Act
            var provider = services.BuildServiceProvider();
            var options  = provider.GetRequiredService<IOptions<RabbitMqPublishOptions<OrderEvent>>>();

            // Assert
            Assert.Contains(services, d => d.ServiceType == typeof(IEventPublishChannel<OrderEvent>));
            Assert.Equal("orders-exchange", options.Value.ExchangeName);
            Assert.Equal("order.created",   options.Value.RoutingKey);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private class OrderEvent { }

        private class CustomRabbitMqConnectionFactory : IRabbitMqConnectionFactory
        {
            public Task<IConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
                => throw new NotImplementedException();
        }
    }
}
