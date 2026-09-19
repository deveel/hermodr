//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

using Grpc.Core;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Hermodr
{
    /// <summary>
    /// Tests for the resilience hardening of the gRPC publish channel:
    /// cancellation must not swallow endpoint failures, non-transport sender
    /// failures must be distinguishable from transport errors, and
    /// configuration errors must enter the regular error pipeline.
    /// </summary>
    public class GrpcChannelResilienceTests
    {
        [Fact]
        public async Task PublishAsync_ConcurrentCancellation_DoesNotMaskEndpointFailures()
        {
            var ct = TestContext.Current.CancellationToken;
            var loggerProvider = new TestGrpc.CollectingLoggerProvider();

            // Endpoint 0 ("slow") is cancelled with the external token mid-flight;
            // endpoint 1 ("fail") faults with a gRPC Unavailable status. The fault
            // must win (Task.WhenAll gives faulted tasks precedence) so the failure
            // is not masked by the concurrent cancellation, and the cancelled
            // endpoint must be reported.
            var services = new ServiceCollection();
            services.AddEventPublisher()
                .AddGrpcEventPublisherChannel(options =>
                {
                    options.Endpoints =
                    [
                        new GrpcEndpoint { Address = "https://slow.example.com:5001", SenderName = "slow" },
                        new GrpcEndpoint { Address = "https://fail.example.com:5001", SenderName = "fail" },
                    ];
                });
            services.RemoveAll<IGrpcEventSender>();
            services.AddSingleton<IGrpcEventSender>(new TestGrpc.FakeSender());
            services.AddKeyedSingleton<IGrpcEventSender>("slow", new CancellingOnTokenSender());
            services.AddKeyedSingleton<IGrpcEventSender>(
                "fail",
                new TestGrpc.FakeSender(new RpcException(new Status(StatusCode.Unavailable, "svc down"))));
            services.RemoveAll<IGrpcChannelFactory>();
            services.AddSingleton<IGrpcChannelFactory>(new TestGrpc.StubChannelFactory());
            services.AddLogging(l => l.AddProvider(loggerProvider));

            var sp = services.BuildServiceProvider();
            var channel = sp.GetRequiredKeyedService<IEventPublishChannel>("");

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromMilliseconds(200));

            var ex = await Assert.ThrowsAsync<GrpcTransportException>(() =>
                channel.PublishAsync(TestGrpc.MakeEvent(), cancellationToken: cts.Token));

            Assert.Equal(StatusCode.Unavailable, ex.StatusCode);

            var entries = loggerProvider.Entries;
            Assert.Contains(entries, e =>
                e.Level == LogLevel.Warning &&
                e.Message.Contains("cancelled for 1 of 2 endpoint deliveries"));
        }

        [Fact]
        public async Task PublishBatchAsync_ConcurrentCancellation_DoesNotMaskEndpointFailures()
        {
            var ct = TestContext.Current.CancellationToken;
            var loggerProvider = new TestGrpc.CollectingLoggerProvider();

            var services = new ServiceCollection();
            services.AddEventPublisher()
                .AddGrpcEventPublisherChannel(options =>
                {
                    options.Endpoints =
                    [
                        new GrpcEndpoint { Address = "https://slow.example.com:5001", SenderName = "slow" },
                        new GrpcEndpoint { Address = "https://fail.example.com:5001", SenderName = "fail" },
                    ];
                });
            services.RemoveAll<IGrpcEventSender>();
            services.AddSingleton<IGrpcEventSender>(new TestGrpc.FakeSender());
            services.AddKeyedSingleton<IGrpcEventSender>("slow", new CancellingOnTokenSender());
            services.AddKeyedSingleton<IGrpcEventSender>(
                "fail",
                new TestGrpc.FakeSender(new RpcException(new Status(StatusCode.Unavailable, "svc down"))));
            services.RemoveAll<IGrpcChannelFactory>();
            services.AddSingleton<IGrpcChannelFactory>(new TestGrpc.StubChannelFactory());
            services.AddLogging(l => l.AddProvider(loggerProvider));

            var sp = services.BuildServiceProvider();
            var channel = (IBatchEventPublishChannel)sp.GetRequiredKeyedService<IEventPublishChannel>("");

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromMilliseconds(200));

            await Assert.ThrowsAsync<GrpcTransportException>(() =>
                channel.PublishBatchAsync([TestGrpc.MakeEvent()], cancellationToken: cts.Token));

            Assert.Contains(loggerProvider.Entries, e =>
                e.Level == LogLevel.Warning &&
                e.Message.Contains("cancelled for 1 of 2 endpoint deliveries"));
        }

        [Fact]
        public async Task PublishAsync_AllEndpointsCancelled_PropagatesCancellation()
        {
            var ct = TestContext.Current.CancellationToken;
            var loggerProvider = new TestGrpc.CollectingLoggerProvider();

            var services = new ServiceCollection();
            services.AddEventPublisher()
                .AddGrpcEventPublisherChannel(options =>
                {
                    options.Endpoints =
                    [
                        TestGrpc.MakeEndpoint("https://a.example.com:5001"),
                        TestGrpc.MakeEndpoint("https://b.example.com:5001"),
                    ];
                });
            services.RemoveAll<IGrpcEventSender>();
            services.AddSingleton<IGrpcEventSender>(new CancellingOnTokenSender());
            services.RemoveAll<IGrpcChannelFactory>();
            services.AddSingleton<IGrpcChannelFactory>(new TestGrpc.StubChannelFactory());
            services.AddLogging(l => l.AddProvider(loggerProvider));

            var sp = services.BuildServiceProvider();
            var channel = sp.GetRequiredKeyedService<IEventPublishChannel>("");

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromMilliseconds(100));

            // Pure cancellation (no endpoint faults) must propagate as cancellation,
            // not as an empty GrpcPublishException ("failed for 0 of N endpoints").
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                channel.PublishAsync(TestGrpc.MakeEvent(), cancellationToken: cts.Token));

            Assert.DoesNotContain(loggerProvider.Entries, e =>
                e.Message.Contains("superseded by the cancellation"));
            Assert.DoesNotContain(loggerProvider.Entries, e =>
                e.Level == LogLevel.Error);
        }

        [Fact]
        public async Task PublishAsync_DeadlineOceWithNoExternalCancellation_SurfacesAsGrpcTransportException()
        {
            var ct = TestContext.Current.CancellationToken;

            // The sender throws an OperationCanceledException while the external
            // token is NOT cancelled (simulating a deadline/transport timeout):
            // the channel must classify it as a transport failure, not cancellation.
            var (channel, _, _) = TestGrpc.BuildChannel(
                [TestGrpc.MakeEndpoint()],
                throwOnCall: new OperationCanceledException("deadline hit"));

            var ex = await Assert.ThrowsAsync<GrpcTransportException>(() =>
                channel.PublishAsync(TestGrpc.MakeEvent(), cancellationToken: ct));

            Assert.Contains("deadline hit", ex.Message);
        }

        [Fact]
        public async Task PublishAsync_InvalidHeader_WithPermissiveValidator_ThrowsGrpcPublishException()
        {
            var ct = TestContext.Current.CancellationToken;

            // Defense-in-depth: when DataAnnotations validation is bypassed, an
            // invalid gRPC metadata key must still be surfaced through the regular
            // error pipeline (GrpcPublishException) rather than a raw ArgumentException.
            var endpoint = TestGrpc.MakeEndpoint();
            endpoint.Headers["x invalid"] = "value"; // spaces are rejected by gRPC metadata

            var (channel, sender) = TestGrpc.BuildChannelWithPermissiveValidator([endpoint]);

            var ex = await Assert.ThrowsAsync<GrpcPublishException>(() =>
                channel.PublishAsync(TestGrpc.MakeEvent(), cancellationToken: ct));

            Assert.IsNotType<GrpcTransportException>(ex);
            Assert.Contains("unexpected error", ex.Message);
            Assert.Empty(sender.SentEvents);
        }

        [Fact]
        public async Task PublishAsync_PlaintextHttpEndpoint_LogsWarning()
        {
            var ct = TestContext.Current.CancellationToken;
            var loggerProvider = new TestGrpc.CollectingLoggerProvider();

            var services = new ServiceCollection();
            services.AddEventPublisher()
                .AddGrpcEventPublisherChannel(options =>
                {
                    options.Endpoints = [TestGrpc.MakeEndpoint("http://svc.example.com:5001")];
                });
            services.RemoveAll<IGrpcEventSender>();
            services.AddSingleton<IGrpcEventSender>(new TestGrpc.FakeSender());
            services.RemoveAll<IGrpcChannelFactory>();
            services.AddSingleton<IGrpcChannelFactory>(new TestGrpc.StubChannelFactory());
            services.AddLogging(l => l.AddProvider(loggerProvider));

            var sp = services.BuildServiceProvider();
            var channel = sp.GetRequiredKeyedService<IEventPublishChannel>("");

            // Plaintext http endpoints are allowed but must produce a warning.
            await channel.PublishAsync(TestGrpc.MakeEvent(), cancellationToken: ct);

            Assert.Contains(loggerProvider.Entries, e =>
                e.Level == LogLevel.Warning &&
                e.Message.Contains("plaintext") &&
                e.Message.Contains("http://svc.example.com:5001"));
        }

        /// <summary>
        /// A sender whose calls complete as cancelled exactly when the provided
        /// (linked) cancellation token fires.
        /// </summary>
        private sealed class CancellingOnTokenSender : IGrpcEventSender
        {
            public Task SendAsync(CloudEvent @event, GrpcCallContext context)
                => WaitCancelledAsync(context.CancellationToken);

            public Task SendBatchAsync(IReadOnlyList<CloudEvent> events, GrpcCallContext context)
                => WaitCancelledAsync(context.CancellationToken);

            private static Task WaitCancelledAsync(CancellationToken token)
                => Task.Delay(Timeout.Infinite, token);
        }
    }
}
