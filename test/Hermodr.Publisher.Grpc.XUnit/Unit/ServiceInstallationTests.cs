//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Microsoft.Extensions.DependencyInjection;

namespace Hermodr
{
    public class ServiceInstallationTests
    {
        [Fact]
        public void AddGrpcEventPublisherChannel_RegistersChannelFactory()
        {
            var services = new ServiceCollection();
            services.AddEventPublisher()
                .AddGrpcEventPublisherChannel(options =>
                {
                    options.Endpoints = [new GrpcEndpoint { Address = "https://svc.example.com:5001" }];
                })
                .AddGrpcEventSender<TestGrpc.FakeSender>();

            var sp = services.BuildServiceProvider();
            var factory = sp.GetService<IGrpcChannelFactory>();
            Assert.NotNull(factory);
        }

        [Fact]
        public void AddGrpcEventPublisherChannel_RegistersDefaultHttpClient()
        {
            var services = new ServiceCollection();
            services.AddEventPublisher()
                .AddGrpcEventPublisherChannel(options =>
                {
                    options.Endpoints = [new GrpcEndpoint { Address = "https://svc.example.com:5001" }];
                })
                .AddGrpcEventSender<TestGrpc.FakeSender>();

            var sp = services.BuildServiceProvider();

            var httpClientFactory = sp.GetService<IHttpClientFactory>();
            Assert.NotNull(httpClientFactory);
            var client = httpClientFactory!.CreateClient(GrpcDefaults.HttpClientName);
            Assert.NotNull(client);
        }

        [Fact]
        public void AddGrpcEventPublisherChannel_RegistersKeyedEventPublishChannel()
        {
            var services = new ServiceCollection();
            services.AddEventPublisher()
                .AddGrpcEventPublisherChannel(options =>
                {
                    options.Endpoints = [new GrpcEndpoint { Address = "https://svc.example.com:5001" }];
                })
                .AddGrpcEventSender<TestGrpc.FakeSender>();

            var sp = services.BuildServiceProvider();
            var channel = sp.GetKeyedService<IEventPublishChannel>("");
            Assert.NotNull(channel);
        }

        [Fact]
        public void AddGrpcEventPublisherChannel_RegistersBatchChannel()
        {
            var services = new ServiceCollection();
            services.AddEventPublisher()
                .AddGrpcEventPublisherChannel(options =>
                {
                    options.Endpoints = [new GrpcEndpoint { Address = "https://svc.example.com:5001" }];
                })
                .AddGrpcEventSender<TestGrpc.FakeSender>();

            var sp = services.BuildServiceProvider();

            // IBatchEventPublishChannel is registered explicitly (like the Webhook channel).
            var batchChannel = sp.GetService<IBatchEventPublishChannel>();
            Assert.NotNull(batchChannel);
        }

        [Fact]
        public void AddGrpcEventSender_ReplacesFallbackSender()
        {
            var services = new ServiceCollection();
            services.AddEventPublisher()
                .AddGrpcEventPublisherChannel(options =>
                {
                    options.Endpoints = [new GrpcEndpoint { Address = "https://svc.example.com:5001" }];
                })
                .AddGrpcEventSender<TestGrpc.FakeSender>();

            var sp = services.BuildServiceProvider();
            var sender = sp.GetRequiredService<IGrpcEventSender>();
            Assert.IsType<TestGrpc.FakeSender>(sender);
        }

        [Fact]
        public void AddGrpcEventSender_Named_RegistersKeyedSender()
        {
            var services = new ServiceCollection();
            services.AddEventPublisher()
                .AddGrpcEventPublisherChannel(options =>
                {
                    options.Endpoints = [new GrpcEndpoint { Address = "https://svc.example.com:5001" }];
                })
                .AddGrpcEventSender<TestGrpc.FakeSender>("orders");

            var sp = services.BuildServiceProvider();
            var namedSender = sp.GetKeyedService<IGrpcEventSender>("orders");
            Assert.NotNull(namedSender);
            Assert.IsType<TestGrpc.FakeSender>(namedSender);
        }

        [Fact]
        public async Task WithoutSender_FallbackThrowsHelpfulMessage()
        {
            var services = new ServiceCollection();
            services.AddEventPublisher()
                .AddGrpcEventPublisherChannel(options =>
                {
                    options.Endpoints = [new GrpcEndpoint { Address = "https://svc.example.com:5001" }];
                });

            var sp = services.BuildServiceProvider();
            var sender = sp.GetRequiredService<IGrpcEventSender>();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sender.SendAsync(TestGrpc.MakeEvent(),
                    new GrpcCallContext(null!, new Grpc.Core.Metadata(),
                        DateTime.UtcNow, CancellationToken.None)));

            Assert.Contains("IGrpcEventSender", ex.Message);
        }

        [Fact]
        public void ConfigureGrpcChannel_RegistersNamedHttpClient()
        {
            var services = new ServiceCollection();
            services.AddEventPublisher()
                .AddGrpcEventPublisherChannel(options =>
                {
                    options.Endpoints = [new GrpcEndpoint
                    {
                        Address = "https://svc.example.com:5001",
                        HttpClientName = "custom-grpc",
                    }];
                })
                .AddGrpcEventSender<TestGrpc.FakeSender>()
                .ConfigureGrpcChannel("custom-grpc", b =>
                {
                    b.ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(5));
                });

            var sp = services.BuildServiceProvider();
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var client = httpClientFactory.CreateClient("custom-grpc");
            Assert.Equal(TimeSpan.FromSeconds(5), client.Timeout);
        }
    }
}
