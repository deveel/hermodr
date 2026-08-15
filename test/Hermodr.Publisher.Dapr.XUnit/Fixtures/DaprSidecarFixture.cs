//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Dapr.Testcontainers.Common.Options;
using Dapr.Testcontainers.Containers.Dapr;

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;

namespace Hermodr
{
    /// <summary>
    /// An xUnit class fixture that starts a Redis container and a Dapr
    /// <c>daprd</c> sidecar wired with a Redis pub/sub component, exposing
    /// the gRPC endpoint of the sidecar for use by <see cref="Dapr.Client.DaprClient"/>.
    /// </summary>
    /// <remarks>
    /// The fixture requires Docker to be available on the host. Tests that use
    /// it are gated behind the <c>Category=Integration</c> trait so they can
    /// be skipped in fast local runs and CI without Docker.
    /// </remarks>
    public sealed class DaprSidecarFixture : IAsyncLifetime
    {
        private static readonly string ComponentsDir =
            Path.Join(AppContext.BaseDirectory, "Fixtures", "components");

        private readonly INetwork _network;
        private readonly IContainer _redis;
        private readonly DaprdContainer _daprd;

        /// <summary>
        /// Indicates whether the sidecar (and the underlying Redis container)
        /// started successfully. When <c>false</c>, tests that use this fixture
        /// skip themselves with a clear message rather than failing.
        /// </summary>
        public bool SidecarStarted { get; private set; }

        public DaprSidecarFixture()
        {
            _network = new NetworkBuilder()
                .WithName("hermodr-dapr-" + Guid.NewGuid().ToString("n")[..24])
                .Build();

            // The pubsub.yaml component references the Redis host as "redis:6379",
            // so the container is registered on the shared network with the
            // "redis" alias to make it resolvable by daprd.
            _redis = new ContainerBuilder("redis:alpine")
                .WithName("hermodr-redis-" + Guid.NewGuid().ToString("N"))
                .WithNetwork(_network)
                .WithNetworkAliases("redis")
                .WithPortBinding(6379, assignRandomHostPort: true)
                .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(6379))
                .Build();

            var runtimeOptions = new DaprRuntimeOptions("1.18.2")
                .WithAppId("hermodr-test-app")
                .WithAppProtocol("http")
                .WithLogLevel(DaprLogLevel.Debug);

            _daprd = new DaprdContainer(
                appId: "hermodr-test-app",
                componentsHostFolder: ComponentsDir,
                runtimeOptions,
                _network,
                placementHostAndPort: null,
                schedulerHostAndPort: null,
                daprHttpPort: null,
                daprGrpcPort: null,
                logDirectory: string.Empty,
                configFilePath: null);
        }

        /// <summary>
        /// The gRPC endpoint of the running <c>daprd</c> sidecar
        /// (e.g. <c>http://localhost:5xxxx</c>).
        /// </summary>
        public string GrpcEndpoint => "http://localhost:" + _daprd.GrpcPort;

        /// <summary>
        /// The pub/sub component name configured on the sidecar (matches the
        /// <c>Fixtures/components/pubsub.yaml</c> resource).
        /// </summary>
        public const string PubSubName = "hermodr-pubsub";

        public async ValueTask InitializeAsync()
        {
            try
            {
                await _network.CreateAsync();
                await _redis.StartAsync();
                await _daprd.StartAsync();
                SidecarStarted = true;
            }
            catch (Exception)
            {
                // Most likely Docker is unavailable, the daprio/daprd image
                // cannot be pulled, or the test environment does not provide
                // a Dapr placement service. Tests that use this fixture will
                // skip themselves via SidecarStarted=false.
                SidecarStarted = false;
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _daprd.DisposeAsync();
            await _redis.DisposeAsync();
            await _network.DeleteAsync();
        }
    }
}