//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

using Dapr.Client;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Hermodr
{
    /// <summary>
    /// Integration tests for the Dapr publish channel using a real
    /// <c>daprd</c> sidecar (backed by a Redis pub/sub component) started
    /// through <c>Dapr.Testcontainers</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These tests require Docker on the host. They are gated behind the
    /// <c>Category=Integration</c> trait; run them selectively (e.g.
    /// <c>dotnet test --filter "Category=Integration"</c>) and exclude them
    /// from fast CI builds with <c>--filter "Category!=Integration"</c>.
    /// </para>
    /// <para>
    /// The integration suite verifies the end-to-end pipeline
    /// (EventPublisher -&gt; Dapr channel -&gt; daprd gRPC -&gt; Redis pub/sub)
    /// by asserting that publishes complete without raising a transport error.
    /// </para>
    /// </remarks>
    [Trait("Channel", "Dapr")]
    [Trait("Feature", "Dapr")]
    [Trait("Category", "Integration")]
    [Trait("Kind", "Integration")]
    public class DaprChannelContainerTests : IClassFixture<DaprSidecarFixture>, IAsyncLifetime
    {
        private readonly DaprSidecarFixture _fixture;
        private readonly IServiceProvider? _services;

        public DaprChannelContainerTests(DaprSidecarFixture fixture, ITestOutputHelper outputHelper)
        {
            _fixture = fixture;

            // When the Dapr sidecar did not start (e.g. Docker unavailable on
            // the CI host), the fixture is reported as not-started and the
            // tests skip themselves via Assert.SkipUnless below. Skip the
            // DaprChannel/DI construction here too, otherwise building a
            // DaprClient against the placeholder "http://localhost:0" endpoint
            // could throw in the test class ctor and turn a skip into a fail.
            if (!fixture.SidecarStarted)
                return;

            var sidecarDaprClient = new DaprClientBuilder()
                .UseGrpcEndpoint(fixture.GrpcEndpoint)
                .Build();

            var clientBuilder = new FixedDaprClientBuilder(sidecarDaprClient);

            var services = new ServiceCollection();
            services.AddLogging(b => b.AddXUnit(outputHelper).SetMinimumLevel(LogLevel.Debug));
            services.AddSingleton<IDaprClientBuilder>(clientBuilder);

            services.AddEventPublisher(options =>
            {
                options.Source = new Uri("https://api.svc.deveel.com/test-service");
                options.DataSchemaBaseUri = new Uri("https://example.com/events/schema");
            })
            .AddDapr(options =>
            {
                options.PubSubName = DaprSidecarFixture.PubSubName;
                options.Topic = "integration-topic";
            });

            _services = services.BuildServiceProvider();
        }

        private EventPublisher Publisher => _services!.GetRequiredService<EventPublisher>();

        [Fact]
        public async Task PublishEventThroughDaprSidecar()
        {
            Assert.SkipUnless(_fixture.SidecarStarted,
                "Dapr sidecar fixture did not start (Docker unavailable or daprd image missing).");

            var ct = TestContext.Current.CancellationToken;
            var cloudEvent = new CloudEvent
            {
                Type = "person.created",
                Source = new Uri("https://api.svc.deveel.com/test-service"),
                Id = Guid.NewGuid().ToString("N"),
                Time = DateTimeOffset.UtcNow,
                DataContentType = "application/json",
                Subject = "integration-topic",
                Data = "{\"first_name\":\"John\",\"last_name\":\"Doe\"}",
            };

            await Publisher.PublishEventAsync(cloudEvent, cancellationToken: ct);
        }

        [Fact]
        public async Task PublishMultipleEventsThroughDaprSidecar()
        {
            Assert.SkipUnless(_fixture.SidecarStarted,
                "Dapr sidecar fixture did not start (Docker unavailable or daprd image missing).");

            var ct = TestContext.Current.CancellationToken;
            const int count = 10;

            for (var i = 0; i < count; i++)
            {
                var cloudEvent = new CloudEvent
                {
                    Type = "person.created",
                    Source = new Uri("https://api.svc.deveel.com/test-service"),
                    Id = Guid.NewGuid().ToString("N"),
                    Time = DateTimeOffset.UtcNow,
                    DataContentType = "application/json",
                    Subject = "integration-topic",
                    Data = $"{{\"index\":{i}}}",
                };

                await Publisher.PublishEventAsync(cloudEvent, cancellationToken: ct);
            }
        }

        public ValueTask InitializeAsync() => ValueTask.CompletedTask;

        public ValueTask DisposeAsync()
        {
            (_services as IDisposable)?.Dispose();
            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// An <see cref="IDaprClientBuilder"/> that returns a single fixed
        /// <see cref="DaprClient"/> instance, used to direct the channel at the
        /// test sidecar regardless of <see cref="DaprPublishOptions.DaprClientName"/>.
        /// </summary>
        private sealed class FixedDaprClientBuilder : IDaprClientBuilder
        {
            private readonly DaprClient _client;

            public FixedDaprClientBuilder(DaprClient client)
            {
                _client = client;
            }

            public DaprClient CreateClient(string? name = null) => _client;
        }
    }
}