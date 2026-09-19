//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Hermodr
{
    public class GrpcPublishChannelExtendedTests
    {
        [Fact]
        public async Task PublishBatchAsync_NullEvents_ThrowsArgumentException()
        {
            var ct = TestContext.Current.CancellationToken;
            var (channel, _, _) = TestGrpc.BuildChannel([TestGrpc.MakeEndpoint()]);
            var batchChannel = (IBatchEventPublishChannel)channel;

            await Assert.ThrowsAsync<ArgumentException>(() =>
                batchChannel.PublishBatchAsync(null!, cancellationToken: ct));
        }

        [Fact]
        public async Task PublishBatchAsync_SingleFailure_RethrowsOriginalException()
        {
            var ct = TestContext.Current.CancellationToken;

            // Two endpoints, one succeeds and one throws. The single-failure path
            // should rethrow the original exception (not wrap in GrpcPublishException).
            var sender = new ConditionalBatchSender(failIndex: 1);
            var channelFactory = new TestGrpc.StubChannelFactory();

            var services = new ServiceCollection();
            services.AddEventPublisher()
                .AddGrpcEventPublisherChannel(options =>
                {
                    options.Endpoints = new[]
                    {
                        TestGrpc.MakeEndpoint("https://svc1.example.com:5001"),
                        TestGrpc.MakeEndpoint("https://svc2.example.com:5001"),
                    };
                });
            services.RemoveAll<IGrpcEventSender>();
            services.AddSingleton<IGrpcEventSender>(sender);
            services.RemoveAll<IGrpcChannelFactory>();
            services.AddSingleton<IGrpcChannelFactory>(channelFactory);
            var sp = services.BuildServiceProvider();
            var channel = sp.GetRequiredKeyedService<IEventPublishChannel>("");
            var batchChannel = (IBatchEventPublishChannel)channel;

            var events = new[] { TestGrpc.MakeEvent() };

            // With a single failure (one of two endpoints), the channel rethrows
            // the wrapped per-endpoint failure via ExceptionDispatchInfo. The
            // conditional sender throws InvalidOperationException, which the
            // channel classifies as a non-transport failure (GrpcPublishException).
            await Assert.ThrowsAsync<GrpcPublishException>(() =>
                batchChannel.PublishBatchAsync(events, cancellationToken: ct));
        }

        [Fact]
        public async Task PublishAsync_NamedSenderNotFound_ThrowsGrpcPublishException()
        {
            var ct = TestContext.Current.CancellationToken;
            var defaultSender = new TestGrpc.FakeSender();

            var services = new ServiceCollection();
            services.AddEventPublisher()
                .AddGrpcEventPublisherChannel(options =>
                {
                    options.Endpoints = [TestGrpc.MakeEndpoint()];
                });
            services.RemoveAll<IGrpcEventSender>();
            services.AddSingleton<IGrpcEventSender>(defaultSender);
            // No named sender registered for "nonexistent" — must fail fast
            // instead of silently misdirecting the event to the default sender.
            services.RemoveAll<IGrpcChannelFactory>();
            services.AddSingleton<IGrpcChannelFactory>(new TestGrpc.StubChannelFactory());

            var sp = services.BuildServiceProvider();
            var options = sp.GetRequiredService<IOptions<GrpcPublishOptions>>();
            options.Value.Endpoints[0].SenderName = "nonexistent";

            var channel = sp.GetRequiredKeyedService<IEventPublishChannel>("");

            var ex = await Assert.ThrowsAsync<GrpcPublishException>(() =>
                channel.PublishAsync(TestGrpc.MakeEvent(), cancellationToken: ct));

            Assert.Contains("nonexistent", ex.Message);
            Assert.Contains("AddGrpcEventSender", ex.Message);

            // The default sender must not have been used.
            Assert.Empty(defaultSender.SentEvents);
        }

        [Fact]
        public async Task PublishAsync_CustomDeadline_PassedToContext()
        {
            var ct = TestContext.Current.CancellationToken;
            var sender = new TestGrpc.FakeSender();
            var channelFactory = new TestGrpc.StubChannelFactory();

            var customDeadline = TimeSpan.FromSeconds(5);
            var endpoint = TestGrpc.MakeEndpoint();
            endpoint.Deadline = customDeadline;

            var services = new ServiceCollection();
            services.AddEventPublisher()
                .AddGrpcEventPublisherChannel(options => { options.Endpoints = [endpoint]; });
            services.RemoveAll<IGrpcEventSender>();
            services.AddSingleton<IGrpcEventSender>(sender);
            services.RemoveAll<IGrpcChannelFactory>();
            services.AddSingleton<IGrpcChannelFactory>(channelFactory);
            var sp = services.BuildServiceProvider();
            var channel = sp.GetRequiredKeyedService<IEventPublishChannel>("");

            await channel.PublishAsync(TestGrpc.MakeEvent(), cancellationToken: ct);

            Assert.Single(sender.CapturedContexts);
            var context = sender.CapturedContexts[0];
            // The deadline in the context should be approximately Now + customDeadline.
            // We verify it's within a reasonable window of the expected value.
            var expectedDeadline = DateTime.UtcNow + customDeadline;
            Assert.True(Math.Abs((context.Deadline - expectedDeadline).TotalSeconds) < 5,
                $"Deadline {context.Deadline} should be close to {expectedDeadline}");
        }

        [Fact]
        public async Task PublishAsync_PerCallOptions_OverridesEndpoints()
        {
            var ct = TestContext.Current.CancellationToken;
            var sender = new TestGrpc.FakeSender();
            var channelFactory = new TestGrpc.StubChannelFactory();

            var services = new ServiceCollection();
            services.AddEventPublisher()
                .AddGrpcEventPublisherChannel(options =>
                {
                    options.Endpoints = [TestGrpc.MakeEndpoint("https://base.example.com:5001")];
                });
            services.RemoveAll<IGrpcEventSender>();
            services.AddSingleton<IGrpcEventSender>(sender);
            services.RemoveAll<IGrpcChannelFactory>();
            services.AddSingleton<IGrpcChannelFactory>(channelFactory);
            var sp = services.BuildServiceProvider();
            var channel = sp.GetRequiredKeyedService<IEventPublishChannel>("");

            // Per-call override with a different endpoint
            var perCallOptions = new GrpcPublishOptions
            {
                Endpoints = [TestGrpc.MakeEndpoint("https://override.example.com:5001")],
            };

            await channel.PublishAsync(TestGrpc.MakeEvent(), perCallOptions, cancellationToken: ct);

            // The override endpoint should have been used
            Assert.Contains("https://override.example.com:5001", channelFactory.ResolvedAddresses);
            Assert.DoesNotContain("https://base.example.com:5001", channelFactory.ResolvedAddresses);
        }

        [Fact]
        public async Task PublishBatchAsync_WithPermissiveValidator_EmptyEndpoints_ThrowsGrpcPublishException()
        {
            var ct = TestContext.Current.CancellationToken;
            var (channel, _) = TestGrpc.BuildChannelWithPermissiveValidator([]);
            var batchChannel = (IBatchEventPublishChannel)channel;

            // With a permissive validator, DataAnnotations validation is skipped
            // and the channel's defensive empty-endpoints guard fires.
            var ex = await Assert.ThrowsAsync<GrpcPublishException>(() =>
                batchChannel.PublishBatchAsync([TestGrpc.MakeEvent()], cancellationToken: ct));

            Assert.Contains("no endpoints are configured", ex.Message);
        }

        [Fact]
        public async Task PublishCoreAsync_WithPermissiveValidator_EmptyEndpoints_ThrowsGrpcPublishException()
        {
            var ct = TestContext.Current.CancellationToken;
            var (channel, _) = TestGrpc.BuildChannelWithPermissiveValidator([]);

            // With a permissive validator, DataAnnotations validation is skipped
            // and the channel's defensive empty-endpoints guard fires.
            var ex = await Assert.ThrowsAsync<GrpcPublishException>(() =>
                channel.PublishAsync(TestGrpc.MakeEvent(), cancellationToken: ct));

            Assert.Contains("no endpoints are configured", ex.Message);
        }

        /// <summary>
        /// A batch sender that throws for the Nth call (by index) and succeeds
        /// for all others, so we can exercise the single-failure rethrow path.
        /// </summary>
        private sealed class ConditionalBatchSender : IGrpcEventSender
        {
            private int _callIndex;
            private readonly int _failIndex;

            public ConditionalBatchSender(int failIndex) => _failIndex = failIndex;

            public Task SendAsync(CloudEvent @event, GrpcCallContext context)
            {
                var idx = Interlocked.Increment(ref _callIndex) - 1;
                return idx == _failIndex
                    ? Task.FromException(new InvalidOperationException("conditional fail"))
                    : Task.CompletedTask;
            }

            public Task SendBatchAsync(IReadOnlyList<CloudEvent> events, GrpcCallContext context)
            {
                var idx = Interlocked.Increment(ref _callIndex) - 1;
                return idx == _failIndex
                    ? Task.FromException(new InvalidOperationException("conditional batch fail"))
                    : Task.CompletedTask;
            }
        }
    }
}
