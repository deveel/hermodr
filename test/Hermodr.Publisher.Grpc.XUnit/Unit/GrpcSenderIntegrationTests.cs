//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

using Grpc.Core;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hermodr
{
    public class GrpcSenderIntegrationTests
    {
        [Fact]
        public async Task SendAsync_ReceivesCallInvokerAndHeaders()
        {
            var ct = TestContext.Current.CancellationToken;
            var endpoint = TestGrpc.MakeEndpoint();
            endpoint.Headers["x-custom"] = "value123";

            var (channel, sender, _) = TestGrpc.BuildChannel([endpoint]);

            await channel.PublishAsync(TestGrpc.MakeEvent(), cancellationToken: ct);

            Assert.Single(sender.CapturedContexts);
            var context = sender.CapturedContexts[0];
            Assert.NotNull(context.CallInvoker);
            Assert.Equal("value123", context.Headers.Get("x-custom")?.Value);
        }

        [Fact]
        public async Task SendAsync_HonorsPerEndpointSenderName()
        {
            var ct = TestContext.Current.CancellationToken;
            var namedSender = new TestGrpc.FakeSender();
            var defaultSender = new TestGrpc.FakeSender();

            var services = new ServiceCollection();
            services.AddEventPublisher()
                .AddGrpcEventPublisherChannel(options =>
                {
                    options.Endpoints = [TestGrpc.MakeEndpoint()];
                });
            services.RemoveAll<IGrpcEventSender>();
            services.AddSingleton<IGrpcEventSender>(defaultSender);
            services.AddKeyedSingleton<IGrpcEventSender>("orders", namedSender);
            services.RemoveAll<IGrpcChannelFactory>();
            services.AddSingleton<IGrpcChannelFactory>(new TestGrpc.StubChannelFactory());

            var sp = services.BuildServiceProvider();

            // Override the endpoint to use the named sender
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<GrpcPublishOptions>>();
            options.Value.Endpoints[0].SenderName = "orders";

            var channel = sp.GetRequiredKeyedService<IEventPublishChannel>("");

            await channel.PublishAsync(TestGrpc.MakeEvent(), cancellationToken: ct);

            Assert.Single(namedSender.SentEvents);
            Assert.Empty(defaultSender.SentEvents);
        }

        [Fact]
        public async Task SendBatchAsync_ReceivesAllEvents()
        {
            var ct = TestContext.Current.CancellationToken;
            var (channel, sender, _) = TestGrpc.BuildChannel([TestGrpc.MakeEndpoint()]);
            var batchChannel = (IBatchEventPublishChannel)channel;

            var events = new[]
            {
                TestGrpc.MakeEvent("type1"),
                TestGrpc.MakeEvent("type2"),
                TestGrpc.MakeEvent("type3"),
            };

            await batchChannel.PublishBatchAsync(events, cancellationToken: ct);

            Assert.Single(sender.SentBatches);
            Assert.Equal(3, sender.SentBatches[0].Count);
        }

        [Fact]
        public async Task SendBatchAsync_FansOutToAllEndpoints()
        {
            var ct = TestContext.Current.CancellationToken;
            var endpoints = new[]
            {
                TestGrpc.MakeEndpoint("https://svc1.example.com:5001"),
                TestGrpc.MakeEndpoint("https://svc2.example.com:5001"),
            };
            var (channel, sender, _) = TestGrpc.BuildChannel(endpoints);
            var batchChannel = (IBatchEventPublishChannel)channel;

            var events = new[] { TestGrpc.MakeEvent(), TestGrpc.MakeEvent() };

            await batchChannel.PublishBatchAsync(events, cancellationToken: ct);

            Assert.Equal(2, sender.SentBatches.Count);
        }

        [Fact]
        public async Task SendBatchAsync_EmptyBatch_Throws()
        {
            var ct = TestContext.Current.CancellationToken;
            var (channel, _, _) = TestGrpc.BuildChannel([TestGrpc.MakeEndpoint()]);
            var batchChannel = (IBatchEventPublishChannel)channel;

            await Assert.ThrowsAsync<ArgumentException>(() =>
                batchChannel.PublishBatchAsync([], cancellationToken: ct));
        }

        [Fact]
        public async Task SendBatchAsync_SenderThrows_SurfacesAsGrpcPublishException()
        {
            var ct = TestContext.Current.CancellationToken;
            var (channel, _, _) = TestGrpc.BuildChannel(
                [TestGrpc.MakeEndpoint()],
                throwOnCall: new InvalidOperationException("batch-fail"));
            var batchChannel = (IBatchEventPublishChannel)channel;

            var events = new[] { TestGrpc.MakeEvent() };

            // Non-transport sender failures surface as the base GrpcPublishException.
            var ex = await Assert.ThrowsAsync<GrpcPublishException>(() =>
                batchChannel.PublishBatchAsync(events, cancellationToken: ct));

            Assert.IsNotType<GrpcTransportException>(ex);
            Assert.Contains("batch-fail", ex.Message);
        }
    }
}
