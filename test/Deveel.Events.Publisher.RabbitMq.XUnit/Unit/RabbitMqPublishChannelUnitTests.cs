// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using CloudNative.CloudEvents;

using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

using RabbitMQ.Client;

using System.Text.Json;

namespace Deveel.Events
{
    /// <summary>
    /// Unit tests for <see cref="RabbitMqPublishChannel"/> that exercise error paths
    /// and guard conditions without requiring a live RabbitMQ broker.
    /// A substituted <see cref="IConnection"/> is pre-registered in DI so that the
    /// channel can be constructed without making any network calls.
    /// </summary>
    [Trait("Channel", "RabbitMQ")]
    [Trait("Function", "Publish")]
    [Trait("Kind", "Unit")]
    public static class RabbitMqPublishChannelUnitTests
    {
        private static readonly DateTimeOffset FixedNow = new(2026, 01, 15, 12, 00, 00, TimeSpan.Zero);

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// Builds a service provider with a fake (NSubstitute) <see cref="IConnection"/>
        /// so that <see cref="RabbitMqPublishChannel"/> can be resolved without a broker.
        /// </summary>
        private static IServiceProvider BuildServices(Action<RabbitMqPublishOptions> configure)
        {
            var fakeConnection = Substitute.For<IConnection>();

            var services = new ServiceCollection();
            // Pre-register the fake connection so that the TryAddSingleton inside
            // AddRabbitMq does not attempt to open a real AMQP connection.
            services.AddSingleton<IConnection>(fakeConnection);
            services.AddEventPublisher()
                    .AddRabbitMq(configure);

            return services.BuildServiceProvider();
        }

        private static CloudEvent MakeCloudEvent(string? exchange = null, string? routingKey = null)
        {
            var evt = new CloudEvent
            {
                Type   = "test.event",
                Source = new Uri("https://test.example.com"),
                Id     = Guid.NewGuid().ToString("N"),
                Time   = FixedNow,
                DataContentType = "application/json",
                Data   = JsonSerializer.Serialize(new { Value = 42 })
            };

            if (exchange   != null) evt["amqpexchange"]   = exchange;
            if (routingKey != null) evt["amqproutingkey"] = routingKey;

            return evt;
        }

        /// <summary>
        /// Resolves the single <see cref="IEventPublishChannel"/> registered by
        /// <c>AddRabbitMq()</c> from the default (unnamed) pipeline slot.
        /// Channels are registered as keyed services keyed by the pipeline name
        /// (<see cref="string.Empty"/> for the default pipeline), so we must use
        /// <see cref="IServiceProviderIsKeyedService"/> / GetRequiredKeyedService.
        /// </summary>
        private static IEventPublishChannel GetChannel(IServiceProvider provider)
            => provider.GetRequiredKeyedService<IEventPublishChannel>(string.Empty);

        // ── Missing exchange name ─────────────────────────────────────────────

        [Fact]
        public static async Task PublishAsync_MissingExchangeName_ThrowsInvalidOperationException()
        {
            var provider = BuildServices(opts =>
            {
                opts.ConnectionString  = "amqp://guest:guest@localhost/";
                // No ExchangeName set in options
                opts.RoutingKey        = "test.key";
                opts.PublisherConfirms = false;
            });

            var channel = GetChannel(provider);

            // CloudEvent also has no amqpexchange attribute
            var cloudEvent = MakeCloudEvent(exchange: null, routingKey: "test.key");

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => channel.PublishAsync(cloudEvent, null, CancellationToken.None));
        }

        [Fact]
        public static async Task PublishAsync_ExchangeNameSetInOptions_NoExceptionRaisedForMissingExchangeInEvent()
        {
            // When ExchangeName and RoutingKey are set in options, a CloudEvent without
            // AMQP extension attributes must fall back to those option values and must NOT
            // throw InvalidOperationException for a "missing exchange name".
            // Because IConnection / IChannel are NSubstitute stubs that silently accept
            // all calls, the publish actually completes successfully (null exception).
            var provider = BuildServices(opts =>
            {
                opts.ConnectionString  = "amqp://guest:guest@localhost/";
                opts.ExchangeName      = "my-exchange";
                opts.RoutingKey        = "my-key";
                opts.PublisherConfirms = false;
            });

            var channel = GetChannel(provider);

            // CloudEvent has no amqpexchange / amqproutingkey attributes → both fall
            // back to the option values, so the guard conditions are satisfied and
            // the NSubstitute broker stub completes the publish without any error.
            var cloudEvent = MakeCloudEvent(exchange: null, routingKey: null);

            var ex = await Record.ExceptionAsync(
                () => channel.PublishAsync(cloudEvent, null, CancellationToken.None));

            // NSubstitute stubs every method, so no exception is expected.
            Assert.Null(ex);
        }

        // ── Missing routing key ───────────────────────────────────────────────

        [Fact]
        public static async Task PublishAsync_MissingRoutingKey_ThrowsInvalidOperationException()
        {
            var provider = BuildServices(opts =>
            {
                opts.ConnectionString  = "amqp://guest:guest@localhost/";
                opts.ExchangeName      = "test-exchange";
                // No RoutingKey set in options
                opts.PublisherConfirms = false;
            });

            var channel = GetChannel(provider);

            // CloudEvent also has no amqproutingkey attribute
            var cloudEvent = MakeCloudEvent(exchange: "test-exchange", routingKey: null);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => channel.PublishAsync(cloudEvent, null, CancellationToken.None));
        }

        // ── Disposed channel ──────────────────────────────────────────────────

        [Fact]
        public static async Task PublishAsync_WhenChannelDisposed_ThrowsObjectDisposedException()
        {
            var provider = BuildServices(opts =>
            {
                opts.ConnectionString  = "amqp://guest:guest@localhost/";
                opts.ExchangeName      = "test-exchange";
                opts.RoutingKey        = "test.key";
                opts.PublisherConfirms = false;
            });

            var channel = GetChannel(provider);

            // Dispose the underlying RabbitMQ channel implementation.
            if (channel is IAsyncDisposable asyncDisposable)
                await asyncDisposable.DisposeAsync();
            else if (channel is IDisposable disposable)
                disposable.Dispose();

            var cloudEvent = MakeCloudEvent("test-exchange", "test.key");

            await Assert.ThrowsAsync<ObjectDisposedException>(
                () => channel.PublishAsync(cloudEvent, null, CancellationToken.None));
        }

        // ── Dispose idempotency ───────────────────────────────────────────────

        [Fact]
        public static async Task DisposeAsync_WhenCalledTwice_IsIdempotent()
        {
            var provider = BuildServices(opts =>
            {
                opts.ConnectionString  = "amqp://guest:guest@localhost/";
                opts.ExchangeName      = "ex";
                opts.RoutingKey        = "rk";
                opts.PublisherConfirms = false;
            });

            var channel = GetChannel(provider);

            if (channel is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
                // Second dispose must not throw.
                var ex = await Record.ExceptionAsync(() => asyncDisposable.DisposeAsync().AsTask());
                Assert.Null(ex);
            }
        }

        [Fact]
        public static void Dispose_WhenCalledTwice_IsIdempotent()
        {
            var provider = BuildServices(opts =>
            {
                opts.ConnectionString  = "amqp://guest:guest@localhost/";
                opts.ExchangeName      = "ex";
                opts.RoutingKey        = "rk";
                opts.PublisherConfirms = false;
            });

            var channel = GetChannel(provider);

            if (channel is IDisposable disposable)
            {
                disposable.Dispose();
                // Second dispose must not throw.
                var ex = Record.Exception(() => disposable.Dispose());
                Assert.Null(ex);
            }
        }

        // ── MergeOptions ──────────────────────────────────────────────────────

        [Fact]
        public static async Task PublishAsync_WithNullPerCallOptions_UsesChannelDefaults()
        {
            // This exercises the MergeOptions(defaults, null) branch which must return
            // the channel-level defaults unchanged.  We verify indirectly: the exchange
            // name from channel defaults causes the expected validation exception.
            var provider = BuildServices(opts =>
            {
                opts.ConnectionString  = "amqp://guest:guest@localhost/";
                // No ExchangeName → MergeOptions(defaults, null) returns defaults
                // → exchange name is null → InvalidOperationException
                opts.RoutingKey        = "rk";
                opts.PublisherConfirms = false;
            });

            var channel = GetChannel(provider);

            // Passing null per-call options → MergeOptions returns defaults (no exchange)
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => channel.PublishAsync(
                    MakeCloudEvent(exchange: null, routingKey: "rk"),
                    null,          // <-- explicit null per-call options
                    CancellationToken.None));
        }
    }
}



