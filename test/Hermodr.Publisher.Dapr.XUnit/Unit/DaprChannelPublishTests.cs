//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;
using CloudNative.CloudEvents.SystemTextJson;

using Dapr.Client;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using System.Net.Mime;

using NSubstitute;

namespace Hermodr
{
    [Trait("Category", "Unit")]
    [Trait("Layer", "Infrastructure")]
    [Trait("Feature", "Dapr")]
    [Trait("Function", "Publish")]
    public class DaprChannelPublishTests
    {
        private readonly IServiceProvider _services;
        private readonly DaprClient _daprClient;

        public DaprChannelPublishTests(ITestOutputHelper outputHelper)
        {
            (_services, _daprClient) = DaprPublisherFactory.Build(outputHelper);
        }

        private EventPublisher Publisher => _services.GetRequiredService<EventPublisher>();

        [Fact]
        public async Task PublishCloudEvent_CallsPublishByteEventWithPubsubAndTopic()
        {
            var ct = TestContext.Current.CancellationToken;
            var cloudEvent = DaprPublisherFactory.NewCloudEvent();

            await Publisher.PublishEventAsync(cloudEvent, cancellationToken: ct);

            await _daprClient.Received(1).PublishByteEventAsync(
                DaprPublisherFactory.PubSubName,
                DaprPublisherFactory.Topic,
                Arg.Any<ReadOnlyMemory<byte>>(),
                Arg.Any<string>(),
                Arg.Any<Dictionary<string, string>>(),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task PublishCloudEvent_PayloadIsCanonicalStructuredCloudEvent()
        {
            var ct = TestContext.Current.CancellationToken;
            ReadOnlyMemory<byte> capturedBody = default;
            string? capturedContentType = null;

            _daprClient.PublishByteEventAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Do<ReadOnlyMemory<byte>>(b => capturedBody = b),
                Arg.Do<string>(c => capturedContentType = c),
                Arg.Any<Dictionary<string, string>>(),
                Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            var cloudEvent = DaprPublisherFactory.NewCloudEvent();

            await Publisher.PublishEventAsync(cloudEvent, cancellationToken: ct);

            Assert.False(capturedBody.IsEmpty);
            Assert.Equal("application/cloudevents+json", capturedContentType);

            var formatter = new JsonEventFormatter();
            var decoded = formatter.DecodeStructuredModeMessage(
                capturedBody,
                new ContentType(capturedContentType!),
                null);

            Assert.Equal(cloudEvent.Id, decoded.Id);
            Assert.Equal(cloudEvent.Type, decoded.Type);
            Assert.Equal(cloudEvent.Source, decoded.Source);
            Assert.Equal(cloudEvent.Subject, decoded.Subject);
        }

        [Fact]
        public async Task PublishCloudEvent_NullEvent_ThrowsArgumentNullException()
        {
            var ct = TestContext.Current.CancellationToken;
            var channel = _services.GetRequiredKeyedService<IEventPublishChannel>(string.Empty);
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                channel.PublishAsync(null!, null, ct));
        }

        [Fact]
        public async Task PublishCloudEvent_TopicFromEventSubject_WhenChannelTopicNull()
        {
            var ct = TestContext.Current.CancellationToken;
            var (services, daprClient) = DaprPublisherFactory.Build(
                Substitute.For<ITestOutputHelper>(),
                configure: options => options.Topic = null);

            var publisher = services.GetRequiredService<EventPublisher>();

            var cloudEvent = DaprPublisherFactory.NewCloudEvent(subject: "orders");
            await publisher.PublishEventAsync(cloudEvent, cancellationToken: ct);

            await daprClient.Received(1).PublishByteEventAsync(
                DaprPublisherFactory.PubSubName,
                "orders",
                Arg.Any<ReadOnlyMemory<byte>>(),
                Arg.Any<string>(),
                Arg.Any<Dictionary<string, string>>(),
                Arg.Any<CancellationToken>());
        }
    }
}