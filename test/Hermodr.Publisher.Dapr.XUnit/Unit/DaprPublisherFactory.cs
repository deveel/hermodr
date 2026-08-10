//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

using Dapr.Client;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using NSubstitute;

namespace Hermodr
{
    /// <summary>
    /// Shared wiring for Dapr channel unit tests: registers a publisher with the
    /// Dapr channel and a NSubstitute <see cref="DaprClient"/> reachable through
    /// a fake <see cref="IDaprClientBuilder"/>.
    /// </summary>
    internal static class DaprPublisherFactory
    {
        public const string PubSubName = "hermodr-pubsub";
        public const string Topic = "test-topic";

        public static (IServiceProvider Services, DaprClient Client) Build(
            ITestOutputHelper outputHelper,
            Action<DaprPublishOptions>? configure = null,
            bool registerChannel = true)
        {
            var daprClient = Substitute.For<DaprClient>();
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
            services.AddLogging(logging =>
                logging.AddXUnit(outputHelper).SetMinimumLevel(LogLevel.Trace));
            services.AddSingleton(clientBuilder);

            var builder = services.AddEventPublisher(options =>
            {
                options.Source = new Uri("https://api.svc.deveel.com/test-service");
                options.DataSchemaBaseUri = new Uri("https://example.com/events/schema");
            });

            if (registerChannel)
            {
                builder.AddDapr(options =>
                {
                    options.PubSubName = PubSubName;
                    options.Topic = Topic;
                    configure?.Invoke(options);
                });
            }

            return (services.BuildServiceProvider(), daprClient);
        }

        public static CloudEvent NewCloudEvent(string? subject = "test-topic") => new CloudEvent
        {
            Type = "person.created",
            Source = new Uri("https://api.svc.deveel.com/test-service"),
            Id = Guid.NewGuid().ToString("N"),
            Time = DateTimeOffset.UtcNow,
            DataContentType = "application/json",
            Data = "{\"first_name\":\"John\",\"last_name\":\"Doe\"}",
            Subject = subject,
        };
    }
}