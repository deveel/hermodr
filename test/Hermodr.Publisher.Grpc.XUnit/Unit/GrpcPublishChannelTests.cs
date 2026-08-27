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
    public class GrpcPublishChannelTests
    {
        [Fact]
        public async Task PublishAsync_SingleEndpoint_CallsSenderOnce()
        {
            var ct = TestContext.Current.CancellationToken;
            var (channel, sender, _) = TestGrpc.BuildChannel([TestGrpc.MakeEndpoint()]);

            await channel.PublishAsync(TestGrpc.MakeEvent(), cancellationToken: ct);

            Assert.Single(sender.SentEvents);
        }

        [Fact]
        public async Task PublishAsync_MultipleEndpoints_FansOutToAll()
        {
            var ct = TestContext.Current.CancellationToken;
            var endpoints = new[]
            {
                TestGrpc.MakeEndpoint("https://svc1.example.com:5001"),
                TestGrpc.MakeEndpoint("https://svc2.example.com:5001"),
                TestGrpc.MakeEndpoint("https://svc3.example.com:5001"),
            };
            var (channel, sender, _) = TestGrpc.BuildChannel(endpoints);

            await channel.PublishAsync(TestGrpc.MakeEvent(), cancellationToken: ct);

            Assert.Equal(3, sender.SentEvents.Count);
        }

        [Fact]
        public async Task PublishAsync_NoEndpoints_ThrowsValidationException()
        {
            var ct = TestContext.Current.CancellationToken;
            var (channel, _, _) = TestGrpc.BuildChannel([]);

            // The base EventPublishChannel<TOptions> validates options via
            // DataAnnotations before reaching PublishCoreAsync, so an empty
            // endpoint list surfaces as a ValidationException.
            await Assert.ThrowsAsync<System.ComponentModel.DataAnnotations.ValidationException>(() =>
                channel.PublishAsync(TestGrpc.MakeEvent(), cancellationToken: ct));
        }

        [Fact]
        public async Task PublishAsync_SenderThrows_SurfacesAsGrpcTransportException()
        {
            var ct = TestContext.Current.CancellationToken;
            var (channel, _, _) = TestGrpc.BuildChannel(
                [TestGrpc.MakeEndpoint()],
                throwOnCall: new InvalidOperationException("boom"));

            var ex = await Assert.ThrowsAsync<GrpcTransportException>(() =>
                channel.PublishAsync(TestGrpc.MakeEvent(), cancellationToken: ct));

            Assert.Contains("boom", ex.Message);
        }

        [Fact]
        public async Task PublishAsync_MultiEndpointPartialFailure_ThrowsAggregatedException()
        {
            var ct = TestContext.Current.CancellationToken;
            var endpoints = new[]
            {
                TestGrpc.MakeEndpoint("https://svc1.example.com:5001"),
                TestGrpc.MakeEndpoint("https://svc2.example.com:5001"),
            };

            var sender = new ThrowingForEventTypeSender("fail");
            var channelFactory = new TestGrpc.StubChannelFactory();

            var services = new ServiceCollection();
            services.AddEventPublisher()
                .AddGrpcEventPublisherChannel(options => { options.Endpoints = endpoints; });
            services.RemoveAll<IGrpcEventSender>();
            services.AddSingleton<IGrpcEventSender>(sender);
            services.RemoveAll<IGrpcChannelFactory>();
            services.AddSingleton<IGrpcChannelFactory>(channelFactory);
            var sp = services.BuildServiceProvider();
            var channel = sp.GetRequiredKeyedService<IEventPublishChannel>("");

            var ex = await Assert.ThrowsAsync<GrpcPublishException>(() =>
                channel.PublishAsync(TestGrpc.MakeEvent("fail"), cancellationToken: ct));

            Assert.Equal(2, ex.Failures.Count);
        }

        [Fact]
        public async Task PublishAsync_PassesEndpointAddressToChannelFactory()
        {
            var ct = TestContext.Current.CancellationToken;
            var address = "https://svc.example.com:5001";
            var (channel, _, channelFactory) = TestGrpc.BuildChannel([TestGrpc.MakeEndpoint(address)]);

            await channel.PublishAsync(TestGrpc.MakeEvent(), cancellationToken: ct);

            Assert.Contains(address, channelFactory.ResolvedAddresses);
        }

        private sealed class ThrowingForEventTypeSender : IGrpcEventSender
        {
            private readonly string _failType;

            public ThrowingForEventTypeSender(string failType) => _failType = failType;

            public Task SendAsync(CloudEvent @event, GrpcCallContext context)
                => @event.Type == _failType
                    ? Task.FromException(new InvalidOperationException("fail"))
                    : Task.CompletedTask;

            public Task SendBatchAsync(IReadOnlyList<CloudEvent> events, GrpcCallContext context)
                => Task.CompletedTask;
        }
    }
}
