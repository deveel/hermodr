//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Deveel.Events
{
    [Trait("Channel", "MassTransit")]
    [Trait("Function", "TypedOptions")]
    public static class TypedChannelOptionsTests
    {
        // ── Unit tests on Merge() ──────────────────────────────────────────────

        [Fact]
        public static void Merge_BothSet_TypedWins()
        {
            var baseOpts = new MassTransitPublishOptions
            {
                DestinationAddress     = new Uri("rabbitmq://localhost/base-queue"),
                MapAttributesToHeaders = false,
            };

            var typedOpts = new MassTransitPublishOptions
            {
                DestinationAddress     = new Uri("rabbitmq://localhost/typed-queue"),
                MapAttributesToHeaders = true,
            };

            var merged = MassTransitPublishOptions.Merge(baseOpts, typedOpts);

            Assert.Equal(new Uri("rabbitmq://localhost/typed-queue"), merged.DestinationAddress);
            Assert.True(merged.MapAttributesToHeaders);
        }

        [Fact]
        public static void Merge_OnlyBaseSet_BaseValuesUsed()
        {
            var baseOpts  = new MassTransitPublishOptions
            {
                DestinationAddress     = new Uri("rabbitmq://localhost/base-queue"),
                MapAttributesToHeaders = true,
            };
            var typedOpts = new MassTransitPublishOptions(); // all nulls

            var merged = MassTransitPublishOptions.Merge(baseOpts, typedOpts);

            Assert.Equal(new Uri("rabbitmq://localhost/base-queue"), merged.DestinationAddress);
            Assert.True(merged.MapAttributesToHeaders);
        }

        [Fact]
        public static void Merge_OnlyTypedSet_TypedValuesUsed()
        {
            var baseOpts  = new MassTransitPublishOptions(); // all nulls
            var typedOpts = new MassTransitPublishOptions
            {
                DestinationAddress     = new Uri("rabbitmq://localhost/typed-queue"),
                MapAttributesToHeaders = false,
            };

            var merged = MassTransitPublishOptions.Merge(baseOpts, typedOpts);

            Assert.Equal(new Uri("rabbitmq://localhost/typed-queue"), merged.DestinationAddress);
            Assert.False(merged.MapAttributesToHeaders);
        }

        // ── DI / registration tests ────────────────────────────────────────────

        private class OrderPlaced { }

        [Fact]
        public static void AddMassTransit_Typed_RegistersTypedOptionsAndChannel()
        {
            var services = new ServiceCollection();
            services.AddEventPublisher()
                .AddMassTransit(opts => opts.DestinationAddress = new Uri("rabbitmq://localhost/base-queue"))
                .AddMassTransit<OrderPlaced>(opts =>
                {
                    opts.DestinationAddress = new Uri("rabbitmq://localhost/order-queue");
                });

            // Typed options configure action registered
            Assert.Contains(services, d =>
                d.ServiceType == typeof(IConfigureOptions<MassTransitPublishOptions<OrderPlaced>>));

            // Typed channel registered as IEventPublishChannel<OrderPlaced>
            Assert.Contains(services, d =>
                d.ServiceType == typeof(IEventPublishChannel<OrderPlaced>));
        }

        [Fact]
        public static void AddMassTransit_Typed_OptionsAreIndependentFromBase()
        {
            var services = new ServiceCollection();
            services.AddEventPublisher()
                .AddMassTransit(opts =>
                {
                    opts.DestinationAddress     = new Uri("rabbitmq://localhost/base-queue");
                    opts.MapAttributesToHeaders  = true;
                })
                .AddMassTransit<OrderPlaced>(opts =>
                {
                    opts.DestinationAddress = new Uri("rabbitmq://localhost/order-queue");
                });

            var provider = services.BuildServiceProvider();

            var baseOpts  = provider.GetRequiredService<IOptions<MassTransitPublishOptions>>();
            var typedOpts = provider.GetRequiredService<IOptions<MassTransitPublishOptions<OrderPlaced>>>();

            Assert.Equal(new Uri("rabbitmq://localhost/base-queue"),  baseOpts.Value.DestinationAddress);
            Assert.Equal(new Uri("rabbitmq://localhost/order-queue"), typedOpts.Value.DestinationAddress);

            // MapAttributesToHeaders not set in typed opts — raw value should be null (inherit happens at construction)
            Assert.Null(typedOpts.Value.MapAttributesToHeaders);
        }

        [Fact]
        public static void AddMassTransit_Typed_MergeAppliedAtConstruction()
        {
            var baseValue = new MassTransitPublishOptions
            {
                DestinationAddress     = new Uri("rabbitmq://localhost/base-queue"),
                MapAttributesToHeaders = true,
            };
            var typedValue = new MassTransitPublishOptions<OrderPlaced>
            {
                DestinationAddress = new Uri("rabbitmq://localhost/order-queue"),
            };

            var merged = MassTransitPublishOptions.Merge(baseValue, typedValue);

            Assert.Equal(new Uri("rabbitmq://localhost/order-queue"), merged.DestinationAddress);  // typed wins
            Assert.True(merged.MapAttributesToHeaders);                                             // falls back to base
        }
    }
}

