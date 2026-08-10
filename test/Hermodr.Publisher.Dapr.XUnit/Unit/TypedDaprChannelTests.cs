//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Dapr.Client;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using NSubstitute;

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hermodr
{
    [Trait("Category", "Unit")]
    [Trait("Layer", "Infrastructure")]
    [Trait("Feature", "Dapr")]
    [Trait("Function", "TypedChannel")]
    public class TypedDaprChannelTests
    {
        [Event("order.placed", "1.0")]
        private class OrderPlaced
        {
            [JsonPropertyName("order_id")]
            public string OrderId { get; set; } = string.Empty;
        }

        private static (IServiceCollection Services, DaprClient Client) BuildTypedServices(
            Action<DaprPublishOptions> baseConfigure,
            Action<DaprPublishOptions<OrderPlaced>>? typedConfigure = null)
        {
            var daprClient = Substitute.For<DaprClient>();
            daprClient.PublishByteEventAsync(
                Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<string>(),
                Arg.Any<Dictionary<string, string>>(),
                Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            var clientBuilder = Substitute.For<IDaprClientBuilder>();
            clientBuilder.CreateClient(Arg.Any<string?>()).Returns(daprClient);

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton(clientBuilder);
            services.AddEventPublisher(o =>
            {
                o.Source = new Uri("https://example.com");
                o.DataSchemaBaseUri = new Uri("https://example.com/events/schema");
                o.ThrowOnErrors = true;
            })
                .AddDapr(baseConfigure)
                .AddDapr<OrderPlaced>(typedConfigure ?? (_ => { }));

            return (services, daprClient);
        }

        [Fact]
        public void AddDapr_Typed_RegistersTypedOptions()
        {
            var (services, _) = BuildTypedServices(o => { o.PubSubName = "ps"; });

            Assert.Contains(services, d =>
                d.ServiceType == typeof(IConfigureOptions<DaprPublishOptions<OrderPlaced>>));
        }

        [Fact]
        public void AddDapr_Typed_RegistersChannelAsIEventPublishChannelOfT()
        {
            var (services, _) = BuildTypedServices(o => { o.PubSubName = "ps"; });

            Assert.Contains(services, d =>
                d.ServiceType == typeof(IEventPublishChannel<OrderPlaced>));
        }

        [Fact]
        public void AddDapr_Typed_RegistersIEventPublishChannel()
        {
            var (services, _) = BuildTypedServices(o => { o.PubSubName = "ps"; });

            Assert.Contains(services, d =>
                d.ServiceType == typeof(IEventPublishChannel));
        }

        [Fact]
        public void AddDapr_Typed_OptionsAreIndependentFromBase()
        {
            var (services, _) = BuildTypedServices(
                o => { o.PubSubName = "base-ps"; o.Topic = "base-topic"; },
                o => { o.Topic = "typed-topic"; });

            var sp = services.BuildServiceProvider();
            var baseOpts = sp.GetRequiredService<IOptions<DaprPublishOptions>>().Value;
            var typedOpts = sp.GetRequiredService<IOptions<DaprPublishOptions<OrderPlaced>>>().Value;

            Assert.Equal("base-ps", baseOpts.PubSubName);
            Assert.Equal("base-topic", baseOpts.Topic);
            Assert.Equal("typed-topic", typedOpts.Topic);

            // PubSubName is not set on the typed override, so its raw value
            // remains the default (""); inheritance happens at construction
            // when Merge() is invoked (see MergeAppliedAtConstruction below).
            Assert.Equal(string.Empty, typedOpts.PubSubName);
        }

        [Fact]
        public void AddDapr_Typed_MergeAppliedAtConstruction()
        {
            var baseValue = new DaprPublishOptions
            {
                PubSubName = "base-ps",
                Topic = "base-topic",
            };
            var typedValue = new DaprPublishOptions<OrderPlaced>
            {
                Topic = "typed-topic",
            };

            var merged = DaprPublishOptions.Merge(baseValue, typedValue);

            Assert.Equal("typed-topic", merged.Topic);          // typed wins
            Assert.Equal("base-ps", merged.PubSubName);        // falls back to base
        }

        [Fact]
        public async Task AddDapr_Typed_PublishUsesMergedOptions()
        {
            var ct = TestContext.Current.CancellationToken;
            var (services, daprClient) = BuildTypedServices(
                o => { o.PubSubName = "base-ps"; o.Topic = "base-topic"; },
                o => { o.Topic = "typed-topic"; });

            var sp = services.BuildServiceProvider();
            var publisher = sp.GetRequiredService<EventPublisher>();

            await publisher.PublishAsync(new OrderPlaced { OrderId = "ord-001" }, cancellationToken: ct);

            await daprClient.Received(1).PublishByteEventAsync(
                "base-ps", "typed-topic",
                Arg.Any<ReadOnlyMemory<byte>>(),
                Arg.Any<string>(),
                Arg.Any<Dictionary<string, string>>(),
                Arg.Any<CancellationToken>());
        }
    }
}