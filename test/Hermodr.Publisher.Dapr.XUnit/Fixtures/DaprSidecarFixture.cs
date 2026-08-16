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
    /// <para>
    /// All Testcontainers resource construction (network, Redis container,
    /// <c>daprd</c> sidecar) is deferred to <see cref="InitializeAsync"/> rather
    /// than performed in the constructor. Testcontainers 4.14 made
    /// <see cref="NetworkBuilder"/> eagerly probe the Docker daemon during
    /// <c>Build()</c>, which raises <c>DockerUnavailableException</c> when
    /// Docker is momentarily unreachable (observed during cold starts on
    /// GitHub Actions Windows runners). Performing the construction inside
    /// <see cref="InitializeAsync"/>'s <c>try/catch</c> lets the fixture mark
    /// itself as not-started so tests skip instead of failing.
    /// </para>
    /// </remarks>
    public sealed class DaprSidecarFixture : IAsyncLifetime
    {
        private static readonly string ComponentsDir =
            Path.Join(AppContext.BaseDirectory, "Fixtures", "components");

        private INetwork? _network;
        private IContainer? _redis;
        private DaprdContainer? _daprd;

        /// <summary>
        /// Indicates whether the sidecar (and the underlying Redis container)
        /// started successfully. When <c>false</c>, tests that use this fixture
        /// skip themselves with a clear message rather than failing.
        /// </summary>
        public bool SidecarStarted { get; private set; }

        public DaprSidecarFixture()
        {
            // Intentionally empty: deferring Testcontainers construction to
            // InitializeAsync so that Docker-unavailable failures are caught
            // there and the fixture is reported as not-started rather than
            // throwing in the constructor.
        }

        /// <summary>
        /// The gRPC endpoint of the running <c>daprd</c> sidecar
        /// (e.g. <c>http://localhost:5xxxx</c>). When the sidecar did not
        /// start, returns <c>http://localhost:0</c> as a placeholder that no
        /// test should ever connect to (the test would skip first).
        /// </summary>
        public string GrpcEndpoint => "http://localhost:" + (_daprd?.GrpcPort ?? 0);

        /// <summary>
        /// The pub/sub component name configured on the sidecar (matches the
        /// <c>Fixtures/components/pubsub.yaml</c> resource).
        /// </summary>
        public const string PubSubName = "hermodr-pubsub";

        public async ValueTask InitializeAsync()
        {
            try
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

                await _network.CreateAsync();
                await _redis.StartAsync();
                await _daprd.StartAsync();
                SidecarStarted = true;
            }
            catch (Exception)
            {
                // Most likely Docker is unavailable (cold runner), the daprio/daprd
                // image cannot be pulled, or the test environment does not provide
                // a Dapr placement service. Tests that use this fixture will skip
                // themselves via SidecarStarted=false.
                SidecarStarted = false;
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_daprd is not null) await _daprd.DisposeAsync();
            if (_redis is not null) await _redis.DisposeAsync();
            if (_network is not null) await _network.DeleteAsync();
        }
    }
}