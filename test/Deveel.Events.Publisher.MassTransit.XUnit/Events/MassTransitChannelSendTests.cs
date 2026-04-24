//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;
using CloudNative.CloudEvents.SystemTextJson;

using MassTransit;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using System.Text.Json;

namespace Deveel.Events
{
    [Trait("Channel", "MassTransit")]
    [Trait("Function", "Send")]
    public class MassTransitChannelSendTests
    {
        private static MassTransitEventPublishChannel BuildChannel(
            MassTransitEventPublishOptions options,
            IPublishEndpoint publishEndpoint,
            ISendEndpointProvider sendEndpointProvider)
        {
            return new MassTransitEventPublishChannel(
                Options.Create(options),
                publishEndpoint,
                sendEndpointProvider,
                validators: null,
                NullLogger<MassTransitEventPublishChannel>.Instance);
        }

        private static CloudEvent MakeSampleEvent() => new CloudEvent
        {
            Type = "person.created",
            Source = new Uri("https://api.svc.deveel.com"),
            Id = Guid.NewGuid().ToString("N"),
            Time = DateTimeOffset.UtcNow,
            DataContentType = "application/json",
            Data = JsonSerializer.Serialize(new { Name = "Alice" }),
        };

        // -------------------------------------------------------------------------
        // Publish path (no DestinationAddress)
        // -------------------------------------------------------------------------

        [Fact]
        public async Task PublishAsync_NoDestination_UsesPublishEndpoint()
        {
            var publishEndpoint = Substitute.For<IPublishEndpoint>();
            var sendProvider = Substitute.For<ISendEndpointProvider>();

            var options = new MassTransitEventPublishOptions();
            var channel = BuildChannel(options, publishEndpoint, sendProvider);

            await channel.PublishAsync(MakeSampleEvent());

            // The Action<> overload is an extension method; NSubstitute intercepts the
            // underlying interface method that takes IPipe<PublishContext<T>>.
            await publishEndpoint.Received(1)
                .Publish<ICloudEventMessage>(
                    Arg.Any<object>(),
                    Arg.Any<IPipe<PublishContext<ICloudEventMessage>>>(),
                    Arg.Any<CancellationToken>());

            await sendProvider.DidNotReceive()
                .GetSendEndpoint(Arg.Any<Uri>());
        }

        // -------------------------------------------------------------------------
        // Send path (DestinationAddress set)
        // -------------------------------------------------------------------------

        [Fact]
        public async Task PublishAsync_WithDestination_UsesSendEndpoint()
        {
            var destination = new Uri("queue:my-events");
            var publishEndpoint = Substitute.For<IPublishEndpoint>();
            var sendProvider = Substitute.For<ISendEndpointProvider>();
            var sendEndpoint = Substitute.For<ISendEndpoint>();

            sendProvider.GetSendEndpoint(destination).Returns(sendEndpoint);

            var options = new MassTransitEventPublishOptions
            {
                DestinationAddress = destination,
            };
            var channel = BuildChannel(options, publishEndpoint, sendProvider);

            await channel.PublishAsync(MakeSampleEvent());

            await sendProvider.Received(1).GetSendEndpoint(destination);
            await sendEndpoint.Received(1)
                .Send<ICloudEventMessage>(
                    Arg.Any<object>(),
                    Arg.Any<IPipe<SendContext<ICloudEventMessage>>>(),
                    Arg.Any<CancellationToken>());

            await publishEndpoint.DidNotReceive()
                .Publish<ICloudEventMessage>(
                    Arg.Any<object>(),
                    Arg.Any<IPipe<PublishContext<ICloudEventMessage>>>(),
                    Arg.Any<CancellationToken>());
        }

        // -------------------------------------------------------------------------
        // Header mapping
        // -------------------------------------------------------------------------

        [Fact]
        public async Task PublishAsync_MapAttributesToHeaders_HeadersAreSet()
        {
            var publishEndpoint = Substitute.For<IPublishEndpoint>();
            var sendProvider = Substitute.For<ISendEndpointProvider>();

            IPipe<PublishContext<ICloudEventMessage>>? capturedPipe = null;

            await publishEndpoint.Publish<ICloudEventMessage>(
                Arg.Any<object>(),
                Arg.Do<IPipe<PublishContext<ICloudEventMessage>>>(p => capturedPipe = p),
                Arg.Any<CancellationToken>());

            var options = new MassTransitEventPublishOptions { MapAttributesToHeaders = true };
            var channel = BuildChannel(options, publishEndpoint, sendProvider);

            var cloudEvent = MakeSampleEvent();
            cloudEvent.Subject = "test-subject";

            await channel.PublishAsync(cloudEvent);

            Assert.NotNull(capturedPipe);

            // Invoke the captured pipe with a mock context to verify headers are set
            var headers = new Dictionary<string, object?>();
            var ctx = Substitute.For<PublishContext<ICloudEventMessage>>();
            // Set up both string? and object? overloads so all Headers.Set calls are captured
            ctx.Headers.When(h => h.Set(Arg.Any<string>(), Arg.Any<string?>()))
                .Do(ci => headers[ci.ArgAt<string>(0)] = ci.ArgAt<string?>(1));
            ctx.Headers.When(h => h.Set(Arg.Any<string>(), Arg.Any<object?>()))
                .Do(ci => headers[ci.ArgAt<string>(0)] = ci.ArgAt<object?>(1));

            await capturedPipe!.Send(ctx);

            Assert.True(headers.ContainsKey("ce-id"), "ce-id header missing");
            Assert.True(headers.ContainsKey("ce-type"), "ce-type header missing");
            Assert.True(headers.ContainsKey("ce-source"), "ce-source header missing");
            Assert.True(headers.ContainsKey("ce-subject"), "ce-subject header missing");
        }

        [Fact]
        public async Task PublishAsync_MapAttributesToHeaders_False_NoHeadersSet()
        {
            var publishEndpoint = Substitute.For<IPublishEndpoint>();
            var sendProvider = Substitute.For<ISendEndpointProvider>();

            IPipe<PublishContext<ICloudEventMessage>>? capturedPipe = null;

            await publishEndpoint.Publish<ICloudEventMessage>(
                Arg.Any<object>(),
                Arg.Do<IPipe<PublishContext<ICloudEventMessage>>>(p => capturedPipe = p),
                Arg.Any<CancellationToken>());

            var options = new MassTransitEventPublishOptions { MapAttributesToHeaders = false };
            var channel = BuildChannel(options, publishEndpoint, sendProvider);

            await channel.PublishAsync(MakeSampleEvent());

            Assert.NotNull(capturedPipe);

            var headers = new Dictionary<string, object?>();
            var ctx = Substitute.For<PublishContext<ICloudEventMessage>>();
            ctx.Headers.When(h => h.Set(Arg.Any<string>(), Arg.Any<string?>()))
                .Do(ci => headers[ci.ArgAt<string>(0)] = ci.ArgAt<string?>(1));
            ctx.Headers.When(h => h.Set(Arg.Any<string>(), Arg.Any<object?>()))
                .Do(ci => headers[ci.ArgAt<string>(0)] = ci.ArgAt<object?>(1));

            await capturedPipe!.Send(ctx);

            Assert.Empty(headers);
        }

        // -------------------------------------------------------------------------
        // Error handling
        // -------------------------------------------------------------------------

        [Fact]
        public async Task PublishAsync_PublishEndpointThrows_WrapsInEventPublishException()
        {
            var publishEndpoint = Substitute.For<IPublishEndpoint>();
            var sendProvider = Substitute.For<ISendEndpointProvider>();

            publishEndpoint
                .Publish<ICloudEventMessage>(
                    Arg.Any<object>(),
                    Arg.Any<IPipe<PublishContext<ICloudEventMessage>>>(),
                    Arg.Any<CancellationToken>())
                .Returns(Task.FromException(new InvalidOperationException("bus error")));

            var options = new MassTransitEventPublishOptions();
            var channel = BuildChannel(options, publishEndpoint, sendProvider);

            await Assert.ThrowsAsync<EventPublishException>(() => channel.PublishAsync(MakeSampleEvent()));
        }

        [Fact]
        public async Task PublishAsync_NullEvent_ThrowsArgumentNullException()
        {
            var publishEndpoint = Substitute.For<IPublishEndpoint>();
            var sendProvider = Substitute.For<ISendEndpointProvider>();
            var options = new MassTransitEventPublishOptions();
            var channel = BuildChannel(options, publishEndpoint, sendProvider);

            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                channel.PublishAsync(null!, CancellationToken.None));
        }

        [Fact]
        public async Task PublishAsync_Cancelled_DoesNotWrapOperationCanceledException()
        {
            var publishEndpoint = Substitute.For<IPublishEndpoint>();
            var sendProvider = Substitute.For<ISendEndpointProvider>();

            var cts = new CancellationTokenSource();
            cts.Cancel();

            publishEndpoint
                .Publish<ICloudEventMessage>(
                    Arg.Any<object>(),
                    Arg.Any<IPipe<PublishContext<ICloudEventMessage>>>(),
                    Arg.Any<CancellationToken>())
                .Returns(Task.FromException(new OperationCanceledException(cts.Token)));

            var options = new MassTransitEventPublishOptions();
            var channel = BuildChannel(options, publishEndpoint, sendProvider);

            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                channel.PublishAsync(MakeSampleEvent(), cts.Token));
        }
    }
}



