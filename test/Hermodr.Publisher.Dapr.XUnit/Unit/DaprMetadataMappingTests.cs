//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

using Dapr.Client;

using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

namespace Hermodr
{
    [Trait("Category", "Unit")]
    [Trait("Layer", "Infrastructure")]
    [Trait("Feature", "Dapr")]
    [Trait("Function", "MetadataMapping")]
    public class DaprMetadataMappingTests
    {
        private static (IServiceProvider Services, DaprClient Client) BuildServices(
            Action<DaprPublishOptions> configure)
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
            services.AddEventPublisher(o => o.Source = new Uri("https://example.com"))
                .AddDapr(configure);

            return (services.BuildServiceProvider(), daprClient);
        }

        private static async Task<Dictionary<string, string>> CaptureMetadata(
            IServiceProvider services,
            DaprClient daprClient,
            CloudEvent cloudEvent)
        {
            Dictionary<string, string> captured = new(StringComparer.Ordinal);

            daprClient.PublishByteEventAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<ReadOnlyMemory<byte>>(),
                Arg.Any<string>(),
                Arg.Do<Dictionary<string, string>>(m =>
                {
                    captured.Clear();
                    foreach (var kv in m) captured[kv.Key] = kv.Value;
                }),
                Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            await services.GetRequiredKeyedService<IEventPublishChannel>(string.Empty)
                .PublishAsync(cloudEvent, null, default);

            return captured;
        }

        [Fact]
        public async Task MapAttributesToMetadataTrue_ProjectsCoreAttributesWithCePrefix()
        {
            var (services, daprClient) = BuildServices(options =>
            {
                options.PubSubName = "ps";
                options.Topic = "topic";
                options.MapAttributesToMetadata = true;
            });

            var captured = await CaptureMetadata(services, daprClient,
                DaprPublisherFactory.NewCloudEvent(subject: "topic"));

            Assert.NotEmpty(captured);
            Assert.Contains("ce-id", captured.Keys);
            Assert.Contains("ce-type", captured.Keys);
            Assert.Contains("ce-source", captured.Keys);
            Assert.Contains("ce-specversion", captured.Keys);
            Assert.Contains("ce-subject", captured.Keys);
            Assert.Contains("ce-datacontenttype", captured.Keys);
            Assert.Contains("ce-time", captured.Keys);
        }

        [Fact]
        public async Task MapAttributesToMetadataFalse_EmptyMetadata()
        {
            var (services, daprClient) = BuildServices(options =>
            {
                options.PubSubName = "ps";
                options.Topic = "topic";
                options.MapAttributesToMetadata = false;
            });

            var captured = await CaptureMetadata(services, daprClient,
                DaprPublisherFactory.NewCloudEvent(subject: "topic"));

            Assert.Empty(captured);
        }

        [Fact]
        public async Task CustomMetadataPrefix_UsedForProjectedAttributes()
        {
            var (services, daprClient) = BuildServices(options =>
            {
                options.PubSubName = "ps";
                options.Topic = "topic";
                options.MapAttributesToMetadata = true;
                options.MetadataPrefix = "cloudEvent-";
            });

            var captured = await CaptureMetadata(services, daprClient,
                DaprPublisherFactory.NewCloudEvent(subject: "topic"));

            Assert.Contains("cloudEvent-id", captured.Keys);
            Assert.Contains("cloudEvent-type", captured.Keys);
            Assert.DoesNotContain("ce-id", captured.Keys);
        }
    }
}