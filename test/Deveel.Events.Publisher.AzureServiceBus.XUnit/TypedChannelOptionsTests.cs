//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Azure.Messaging.ServiceBus;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Deveel.Events
{
    [Trait("Channel", "ServiceBus")]
    [Trait("Function", "TypedOptions")]
    public static class TypedChannelOptionsTests
    {
        // ── Unit tests on Merge() ──────────────────────────────────────────────

        [Fact]
        public static void Merge_BothSet_TypedWins()
        {
            var baseOpts = new ServiceBusPublishOptions
            {
                ConnectionString = "Endpoint=sb://base.servicebus.windows.net/;SharedAccessKeyName=K;SharedAccessKey=base",
                QueueName        = "base-queue",
            };

            var typedOpts = new ServiceBusPublishOptions<OrderPlaced>
            {
                ConnectionString = "Endpoint=sb://typed.servicebus.windows.net/;SharedAccessKeyName=K;SharedAccessKey=typed",
                QueueName        = "typed-queue",
                ClientOptions    = new ServiceBusClientOptions { TransportType = ServiceBusTransportType.AmqpWebSockets },
            };

            var merged = ServiceBusPublishOptions<OrderPlaced>.Merge(baseOpts, typedOpts);

            Assert.Equal("Endpoint=sb://typed.servicebus.windows.net/;SharedAccessKeyName=K;SharedAccessKey=typed",
                merged.ConnectionString);
            Assert.Equal("typed-queue", merged.QueueName);
            Assert.NotNull(merged.ClientOptions);
            Assert.Equal(ServiceBusTransportType.AmqpWebSockets, merged.ClientOptions.TransportType);
        }

        [Fact]
        public static void Merge_OnlyBaseSet_BaseValuesUsed()
        {
            var baseOpts = new ServiceBusPublishOptions
            {
                ConnectionString = "Endpoint=sb://base.servicebus.windows.net/;SharedAccessKeyName=K;SharedAccessKey=base",
                QueueName        = "base-queue",
            };
            var typedOpts = new ServiceBusPublishOptions<OrderPlaced>(); // all nulls

            var merged = ServiceBusPublishOptions<OrderPlaced>.Merge(baseOpts, typedOpts);

            Assert.Equal("Endpoint=sb://base.servicebus.windows.net/;SharedAccessKeyName=K;SharedAccessKey=base",
                merged.ConnectionString);
            Assert.Equal("base-queue", merged.QueueName);
        }

        [Fact]
        public static void Merge_OnlyTypedSet_TypedValuesUsed()
        {
            var baseOpts  = new ServiceBusPublishOptions(); // empty strings
            var typedOpts = new ServiceBusPublishOptions<OrderPlaced>
            {
                ConnectionString = "Endpoint=sb://typed.servicebus.windows.net/;SharedAccessKeyName=K;SharedAccessKey=typed",
                QueueName        = "typed-queue",
            };

            var merged = ServiceBusPublishOptions<OrderPlaced>.Merge(baseOpts, typedOpts);

            Assert.Equal("Endpoint=sb://typed.servicebus.windows.net/;SharedAccessKeyName=K;SharedAccessKey=typed",
                merged.ConnectionString);
            Assert.Equal("typed-queue", merged.QueueName);
        }

        // ── DI / registration tests ────────────────────────────────────────────

        private class OrderPlaced { }

        [Fact]
        public static void AddServiceBus_Typed_RegistersTypedOptionsAndChannel()
        {
            var services = new ServiceCollection();
            services.AddEventPublisher()
                .AddServiceBus(opts =>
                {
                    opts.ConnectionString = "Endpoint=sb://base.servicebus.windows.net/;SharedAccessKeyName=K;SharedAccessKey=base";
                    opts.QueueName = "base-queue";
                })
                .AddServiceBus<OrderPlaced>(opts =>
                {
                    opts.QueueName = "order-queue";
                });

            // Typed options configured
            Assert.Contains(services, d =>
                d.ServiceType == typeof(IConfigureOptions<ServiceBusPublishOptions<OrderPlaced>>));

            // Typed channel registered as IEventPublishChannel<OrderPlaced>
            Assert.Contains(services, d =>
                d.ServiceType == typeof(IEventPublishChannel<OrderPlaced>));
        }

        [Fact]
        public static void AddServiceBus_Typed_OptionsAreIndependentFromBase()
        {
            var services = new ServiceCollection();
            services.AddEventPublisher()
                .AddServiceBus(opts =>
                {
                    opts.ConnectionString = "Endpoint=sb://base.servicebus.windows.net/;SharedAccessKeyName=K;SharedAccessKey=base";
                    opts.QueueName = "base-queue";
                })
                .AddServiceBus<OrderPlaced>(opts =>
                {
                    opts.QueueName = "order-queue";
                });

            var provider = services.BuildServiceProvider();

            var baseOpts  = provider.GetRequiredService<IOptions<ServiceBusPublishOptions>>();
            var typedOpts = provider.GetRequiredService<IOptions<ServiceBusPublishOptions<OrderPlaced>>>();

            Assert.Equal("base-queue",  baseOpts.Value.QueueName);
            Assert.Equal("order-queue", typedOpts.Value.QueueName);

            // Base ConnectionString is NOT automatically copied into the raw typed options
            Assert.Empty(typedOpts.Value.ConnectionString);
        }

        [Fact]
        public static void AddServiceBus_Typed_MergeAppliedAtConstruction()
        {
            var baseValue = new ServiceBusPublishOptions
            {
                ConnectionString = "Endpoint=sb://base.servicebus.windows.net/;SharedAccessKeyName=K;SharedAccessKey=base",
                QueueName        = "base-queue",
            };
            var typedValue = new ServiceBusPublishOptions<OrderPlaced>
            {
                QueueName = "order-queue",
            };

            var merged = ServiceBusPublishOptions<OrderPlaced>.Merge(baseValue, typedValue);

            // ConnectionString falls back to base (typed left it null)
            Assert.Equal("Endpoint=sb://base.servicebus.windows.net/;SharedAccessKeyName=K;SharedAccessKey=base",
                merged.ConnectionString);
            // QueueName comes from typed
            Assert.Equal("order-queue", merged.QueueName);
        }
    }
}

