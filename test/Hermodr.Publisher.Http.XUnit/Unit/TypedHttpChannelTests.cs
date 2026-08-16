//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Text.Json;

using CloudNative.CloudEvents;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Hermodr
{
    [Trait("Channel", "Http")]
    [Trait("Function", "TypedChannel")]
    public class TypedHttpChannelTests
    {
        private static HttpEndpoint MakeBaseEndpoint() => new()
        {
            Url = "https://base.example.com/events",
            HttpClientName = "base-client"
        };

        private static HttpEndpoint MakeTypedEndpoint() => new()
        {
            Url = "https://typed.example.com/events",
            HttpClientName = "typed-client"
        };

        [Fact]
        public void AddHttpEventPublisherChannel_Typed_RegistersTypedOptions()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventPublisher()
                    .AddHttpEventPublisherChannel(o => o.Endpoints = [MakeBaseEndpoint()])
                    .AddHttpEventPublisherChannel<OrderCreated>(o => o.Endpoints = [MakeTypedEndpoint()]);

            Assert.Contains(services, d =>
                d.ServiceType == typeof(IConfigureOptions<HttpPublishOptions<OrderCreated>>));
        }

        [Fact]
        public void AddHttpEventPublisherChannel_Typed_ResolvesTypedOptions()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventPublisher()
                    .AddHttpEventPublisherChannel(o => o.Endpoints = [MakeBaseEndpoint()])
                    .AddHttpEventPublisherChannel<OrderCreated>(o => o.Endpoints = [MakeTypedEndpoint()]);

            var sp = services.BuildServiceProvider();
            var typedOptions = sp.GetRequiredService<IOptions<HttpPublishOptions<OrderCreated>>>().Value;
            var baseOptions = sp.GetRequiredService<IOptions<HttpPublishOptions>>().Value;

            Assert.Single(typedOptions.Endpoints);
            Assert.Equal("https://typed.example.com/events", typedOptions.Endpoints[0].Url);
            Assert.Single(baseOptions.Endpoints);
            Assert.Equal("https://base.example.com/events", baseOptions.Endpoints[0].Url);
        }

        [Fact]
        public void AddHttpEventPublisherChannel_Typed_RegistersChannelAsIEventPublishChannelOfT()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventPublisher()
                    .AddHttpEventPublisherChannel(o => o.Endpoints = [MakeBaseEndpoint()])
                    .AddHttpEventPublisherChannel<OrderCreated>(o => o.Endpoints = [MakeTypedEndpoint()]);

            Assert.Contains(services, d =>
                d.ServiceType == typeof(IEventPublishChannel<OrderCreated>));
        }

        [Fact]
        public void AddHttpEventPublisherChannel_Typed_RegistersKeyedChannel()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventPublisher()
                    .AddHttpEventPublisherChannel(o => o.Endpoints = [MakeBaseEndpoint()])
                    .AddHttpEventPublisherChannel<OrderCreated>(o => o.Endpoints = [MakeTypedEndpoint()]);

            Assert.Contains(services, d =>
                d.ServiceType == typeof(IEventPublishChannel) && d.ServiceKey is not null);
        }

        [Fact]
        public void AddHttpEventPublisherChannel_RegistersHttpInfrastructure()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventPublisher()
                    .AddHttpEventPublisherChannel(o => o.Endpoints = [MakeBaseEndpoint()]);

            Assert.Contains(services, d => d.ServiceType == typeof(IHttpClientFactory));
            Assert.Contains(services, d => d.ServiceType == typeof(IEventPublisher));
        }

        [Fact]
        public async Task PublishAsync_TypedChannel_DeliversToOnlyItsEndpoint()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var requests = new List<string>();
            var handler = new TestHttp.FakeHandler(req =>
            {
                lock (requests)
                    requests.Add(req.RequestUri!.ToString());
                return TestHttp.OK();
            });

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventPublisher()
                    .AddHttpEventPublisherChannel(o => o.Endpoints = [MakeBaseEndpoint()])
                    .AddHttpEventPublisherChannel<OrderCreated>(o => o.Endpoints = [MakeTypedEndpoint()]);

            services.AddHttpClient("base-client")
                .ConfigurePrimaryHttpMessageHandler(() => handler);
            services.AddHttpClient("typed-client")
                .ConfigurePrimaryHttpMessageHandler(() => handler);

            var sp = services.BuildServiceProvider();
            var typedChannel = sp.GetRequiredKeyedService<IEventPublishChannel<OrderCreated>>("");

            var @event = new CloudEvent(CloudEventsSpecVersion.V1_0)
            {
                Type = "order.created",
                Source = new Uri("https://api.example.com/svc"),
                Id = Guid.NewGuid().ToString("N"),
                DataContentType = "application/json",
                Data = JsonSerializer.Serialize(new OrderCreated { OrderId = "ORD-1" })
            };

            await typedChannel.PublishAsync(@event, cancellationToken: cancellationToken);

            lock (requests)
            {
                Assert.Single(requests);
                Assert.Equal("https://typed.example.com/events", requests[0]);
            }
        }

        private sealed class OrderCreated
        {
            public string OrderId { get; set; } = string.Empty;
        }
    }
}