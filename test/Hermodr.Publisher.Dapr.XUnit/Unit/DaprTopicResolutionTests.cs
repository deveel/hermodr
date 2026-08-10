//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Dapr.Client;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using NSubstitute;

namespace Hermodr
{
    [Trait("Category", "Unit")]
    [Trait("Layer", "Infrastructure")]
    [Trait("Feature", "Dapr")]
    [Trait("Function", "TopicResolution")]
    public class DaprTopicResolutionTests
    {
        private static IServiceProvider BuildServices(
            Action<DaprPublishOptions> configure,
            out DaprClient daprClient)
        {
            daprClient = Substitute.For<DaprClient>();
            daprClient.PublishByteEventAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<ReadOnlyMemory<byte>>(),
                Arg.Any<string>(),
                Arg.Any<Dictionary<string, string>>(),
                Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            var clientBuilder = Substitute.For<IDaprClientBuilder>();
            clientBuilder.CreateClient(Arg.Any<string?>()).Returns(daprClient);

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton(clientBuilder);
            services.AddEventPublisher(o => o.Source = new Uri("https://example.com"))
                .AddDapr(configure);

            return services.BuildServiceProvider();
        }

        private static IEventPublishChannel ResolveChannel(IServiceProvider services)
            => services.GetRequiredKeyedService<IEventPublishChannel>(string.Empty);

        [Fact]
        public async Task ChannelTopic_TakesPrecedenceOverSelectorAndSubject()
        {
            var ct = TestContext.Current.CancellationToken;
            var services = BuildServices(options =>
            {
                options.PubSubName = "ps";
                options.Topic = "channel-topic";
                options.TopicSelector = _ => "selector-topic";
            }, out var daprClient);

            await ResolveChannel(services).PublishAsync(
                DaprPublisherFactory.NewCloudEvent(subject: "subject-topic"), null, ct);

            await daprClient.Received(1).PublishByteEventAsync(
                "ps", "channel-topic",
                Arg.Any<ReadOnlyMemory<byte>>(),
                Arg.Any<string>(),
                Arg.Any<Dictionary<string, string>>(),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task TopicSelector_TakesPrecedenceOverSubject_WhenTopicNull()
        {
            var ct = TestContext.Current.CancellationToken;
            var services = BuildServices(options =>
            {
                options.PubSubName = "ps";
                options.Topic = null;
                options.TopicSelector = e => $"selector-{e.Subject}";
            }, out var daprClient);

            await ResolveChannel(services).PublishAsync(
                DaprPublisherFactory.NewCloudEvent(subject: "orders"), null, ct);

            await daprClient.Received(1).PublishByteEventAsync(
                "ps", "selector-orders",
                Arg.Any<ReadOnlyMemory<byte>>(),
                Arg.Any<string>(),
                Arg.Any<Dictionary<string, string>>(),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Subject_IsUsed_WhenTopicAndSelectorAreNull()
        {
            var ct = TestContext.Current.CancellationToken;
            var services = BuildServices(options =>
            {
                options.PubSubName = "ps";
                options.Topic = null;
                options.TopicSelector = null;
            }, out var daprClient);

            await ResolveChannel(services).PublishAsync(
                DaprPublisherFactory.NewCloudEvent(subject: "fallback-topic"), null, ct);

            await daprClient.Received(1).PublishByteEventAsync(
                "ps", "fallback-topic",
                Arg.Any<ReadOnlyMemory<byte>>(),
                Arg.Any<string>(),
                Arg.Any<Dictionary<string, string>>(),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task ThrowsDaprPublishException_WhenTopicUnresolvable()
        {
            var ct = TestContext.Current.CancellationToken;
            var services = BuildServices(options =>
            {
                options.PubSubName = "ps";
                options.Topic = null;
                options.TopicSelector = null;
            }, out _);

            var channel = ResolveChannel(services);
            var cloudEvent = DaprPublisherFactory.NewCloudEvent(subject: null);

            await Assert.ThrowsAsync<DaprPublishException>(() =>
                channel.PublishAsync(cloudEvent, null, ct));
        }

        [Fact]
        public async Task PerCallTopic_OverridesChannelTopic()
        {
            var ct = TestContext.Current.CancellationToken;
            var services = BuildServices(options =>
            {
                options.PubSubName = "ps";
                options.Topic = "channel-topic";
            }, out var daprClient);

            var perCall = new DaprPublishOptions
            {
                PubSubName = "ps",
                Topic = "per-call-topic",
            };

            await ResolveChannel(services).PublishAsync(
                DaprPublisherFactory.NewCloudEvent(), perCall, ct);

            await daprClient.Received(1).PublishByteEventAsync(
                "ps", "per-call-topic",
                Arg.Any<ReadOnlyMemory<byte>>(),
                Arg.Any<string>(),
                Arg.Any<Dictionary<string, string>>(),
                Arg.Any<CancellationToken>());
        }
    }
}