//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Deveel.Events
{
    [Trait("Channel", "RabbitMq")]
    [Trait("Function", "TypedOptions")]
    public static class TypedChannelOptionsTests
    {
        // ── Unit tests on Merge() ──────────────────────────────────────────────

        [Fact]
        public static void Merge_BothSet_TypedWins()
        {
            var baseOpts = new RabbitMqEventPublishOptions
            {
                ConnectionString = "amqp://base:base@localhost/",
                ExchangeName     = "base-exchange",
                QueueName        = "base-queue",
                RoutingKey       = "base-key",
                ClientName       = "BaseClient",
                PersistentMessages = true,
                PublisherConfirms  = true,
                Mandatory          = false,
                MessageFormat      = RabbitMqMessageFormat.Json,
                MessageContent     = RabbitMqMessageContent.CloudEvent,
                ConfirmTimeout     = TimeSpan.FromSeconds(5),
            };

            var typedOpts = new RabbitMqEventPublishOptions
            {
                ConnectionString = "amqp://typed:typed@localhost/",
                ExchangeName     = "typed-exchange",
                QueueName        = "typed-queue",
                RoutingKey       = "typed-key",
                ClientName       = "TypedClient",
                PersistentMessages = false,
                PublisherConfirms  = false,
                Mandatory          = true,
                MessageFormat      = RabbitMqMessageFormat.Json,
                MessageContent     = RabbitMqMessageContent.EventData,
                ConfirmTimeout     = TimeSpan.FromSeconds(10),
            };

            var merged = RabbitMqEventPublishOptions.Merge(baseOpts, typedOpts);

            Assert.Equal("amqp://typed:typed@localhost/", merged.ConnectionString);
            Assert.Equal("typed-exchange", merged.ExchangeName);
            Assert.Equal("typed-queue", merged.QueueName);
            Assert.Equal("typed-key", merged.RoutingKey);
            Assert.Equal("TypedClient", merged.ClientName);
            Assert.False(merged.PersistentMessages);
            Assert.False(merged.PublisherConfirms);
            Assert.True(merged.Mandatory);
            Assert.Equal(RabbitMqMessageContent.EventData, merged.MessageContent);
            Assert.Equal(TimeSpan.FromSeconds(10), merged.ConfirmTimeout);
        }

        [Fact]
        public static void Merge_OnlyBaseSet_BaseValuesUsed()
        {
            var baseOpts = new RabbitMqEventPublishOptions
            {
                ConnectionString = "amqp://base:base@localhost/",
                ExchangeName     = "base-exchange",
                QueueName        = "base-queue",
                RoutingKey       = "base-key",
                PersistentMessages = true,
            };
            var typedOpts = new RabbitMqEventPublishOptions(); // all nulls

            var merged = RabbitMqEventPublishOptions.Merge(baseOpts, typedOpts);

            Assert.Equal("amqp://base:base@localhost/", merged.ConnectionString);
            Assert.Equal("base-exchange", merged.ExchangeName);
            Assert.Equal("base-queue", merged.QueueName);
            Assert.Equal("base-key", merged.RoutingKey);
            Assert.True(merged.PersistentMessages);
        }

        [Fact]
        public static void Merge_OnlyTypedSet_TypedValuesUsed()
        {
            var baseOpts  = new RabbitMqEventPublishOptions(); // all nulls
            var typedOpts = new RabbitMqEventPublishOptions
            {
                ConnectionString = "amqp://typed:typed@localhost/",
                ExchangeName     = "typed-exchange",
                QueueName        = "typed-queue",
                Mandatory        = true,
            };

            var merged = RabbitMqEventPublishOptions.Merge(baseOpts, typedOpts);

            Assert.Equal("amqp://typed:typed@localhost/", merged.ConnectionString);
            Assert.Equal("typed-exchange", merged.ExchangeName);
            Assert.Equal("typed-queue", merged.QueueName);
            Assert.True(merged.Mandatory);
        }

        // ── DI / registration tests ────────────────────────────────────────────

        private class OrderPlaced { }

        [Fact]
        public static void AddRabbitMq_Typed_RegistersTypedOptionsAndChannel()
        {
            var services = new ServiceCollection();
            services.AddEventPublisher()
                .AddRabbitMq(opts => opts.ConnectionString = "amqp://base:base@localhost/")
                .AddRabbitMq<OrderPlaced>(opts =>
                {
                    opts.ExchangeName = "order-exchange";
                    opts.QueueName    = "order-queue";
                });

            // Typed options descriptor present
            Assert.Contains(services, d =>
                d.ServiceType == typeof(IConfigureOptions<RabbitMqEventPublishOptions<OrderPlaced>>));

            // Typed channel registered as IEventPublishChannel<OrderPlaced>
            Assert.Contains(services, d =>
                d.ServiceType == typeof(IEventPublishChannel<OrderPlaced>));
        }

        [Fact]
        public static void AddRabbitMq_Typed_OptionsAreIndependentFromBase()
        {
            var services = new ServiceCollection();
            services.AddEventPublisher()
                .AddRabbitMq(opts =>
                {
                    opts.ConnectionString = "amqp://base:base@localhost/";
                    opts.ExchangeName     = "base-exchange";
                })
                .AddRabbitMq<OrderPlaced>(opts =>
                {
                    opts.ExchangeName = "order-exchange";
                });

            var provider = services.BuildServiceProvider();

            var baseOptions  = provider.GetRequiredService<IOptions<RabbitMqEventPublishOptions>>();
            var typedOptions = provider.GetRequiredService<IOptions<RabbitMqEventPublishOptions<OrderPlaced>>>();

            Assert.Equal("base-exchange",  baseOptions.Value.ExchangeName);
            Assert.Equal("order-exchange", typedOptions.Value.ExchangeName);

            // Base ConnectionString is NOT inherited into the raw typed options —
            // inheriting happens only at channel construction time via Merge().
            Assert.Null(typedOptions.Value.ConnectionString);
        }

        [Fact]
        public static void AddRabbitMq_Typed_MergeAppliedAtConstruction()
        {
            // We can't easily verify the merged options without a working AMQ connection,
            // but we CAN verify that Merge() produces the expected result when called
            // with the same values that DI would supply.
            var baseValue = new RabbitMqEventPublishOptions
            {
                ConnectionString = "amqp://base:base@localhost/",
                ExchangeName     = "base-exchange",
                QueueName        = "base-queue",
            };
            var typedValue = new RabbitMqEventPublishOptions<OrderPlaced>
            {
                ExchangeName = "order-exchange",
            };

            var merged = RabbitMqEventPublishOptions.Merge(baseValue, typedValue);

            Assert.Equal("amqp://base:base@localhost/", merged.ConnectionString); // falls back to base
            Assert.Equal("order-exchange", merged.ExchangeName);                  // typed wins
            Assert.Equal("base-queue",     merged.QueueName);                     // falls back to base
        }
    }
}



