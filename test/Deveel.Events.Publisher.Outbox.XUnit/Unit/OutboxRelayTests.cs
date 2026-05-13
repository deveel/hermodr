//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

using Deveel.Events.Fakes;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using System.Diagnostics;

namespace Deveel.Events
{
    /// <summary>
    /// Tests that verify the outbox relay processor and hosted service correctly
    /// dequeue pending messages and forward them to transport channels.
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Layer", "Infrastructure")]
    [Trait("Feature", "Outbox")]
    public class OutboxRelayTests
    {
        // ── Helpers ──────────────────────────────────────────────────────────

        private static CloudEvent MakeEvent(string type = "relay.test") => new()
        {
            Type   = type,
            Source = new Uri("https://example.com"),
            Id     = Guid.NewGuid().ToString("N"),
        };

        /// <summary>
        /// Builds a service provider pre-wired with:
        /// <list type="bullet">
        ///   <item>Outbox publish channel (write path)</item>
        ///   <item>A <see cref="FakeRelayChannel"/> (transport channel)</item>
        ///   <item>The outbox relay processor</item>
        /// </list>
        /// Both channels are registered in the same (default) publisher pipeline so that
        /// when the relay calls <see cref="IEventPublisher.PublishEventAsync"/> with an
        /// <see cref="OutboxRelayPublishOptions"/> signal the outbox channel self-excludes
        /// and only the transport channel handles the event.
        /// </summary>
        private static (IServiceProvider Provider, FakeOutboxMessageRepository Repository, FakeRelayChannel RelayChannel)
            BuildProviderWithRelay(Action<OutboxRelayOptions>? configureRelay = null)
        {
            var repository   = new FakeOutboxMessageRepository();
            var relayChannel = new FakeRelayChannel();
            var factory      = new FakeOutboxMessageFactory();

            var services = new ServiceCollection();

            // Register outbox channel, relay processor, and transport channel all in the
            // same default publisher pipeline.  The OutboxRelayPublishOptions skip signal
            // prevents the outbox channel from re-persisting events during relay.
            services.AddEventPublisher()
                    .AddOutbox<FakeOutboxMessage>()
                    .WithFactory<FakeOutboxMessageFactory>(ServiceLifetime.Singleton)
                    .WithRepository<FakeOutboxMessageRepository>(ServiceLifetime.Singleton)
                    .WithRelay(opts =>
                    {
                        opts.Interval = TimeSpan.FromMilliseconds(50);
                        configureRelay?.Invoke(opts);
                    })
                    .Services
                    .AddEventPublisher()
                    .AddChannel(relayChannel);

            // Replace factory/repository with pre-built instances.
            services.AddSingleton<IOutboxMessageFactory<FakeOutboxMessage>>(factory);
            services.AddSingleton<IOutboxMessageRepository<FakeOutboxMessage>>(repository);

            var provider = services.BuildServiceProvider();
            return (provider, repository, relayChannel);
        }

        // ── OutboxRelayProcessor: happy path ─────────────────────────────────

        [Fact]
        public async Task Processor_NoPendingMessages_PublishesNothing()
        {
            var (provider, _, relayChannel) = BuildProviderWithRelay();
            var processor = provider.GetRequiredService<IOutboxRelayProcessor>();

            await processor.ProcessPendingMessagesAsync(TestContext.Current.CancellationToken);

            Assert.Empty(relayChannel.Published);
        }

        [Fact]
        public async Task Processor_OnePendingMessage_PublishesToRelayChannel()
        {
            var (provider, repository, relayChannel) = BuildProviderWithRelay();

            var evt = MakeEvent("order.created");
            repository.SeedAsync(new FakeOutboxMessage(evt));

            var processor = provider.GetRequiredService<IOutboxRelayProcessor>();
            await processor.ProcessPendingMessagesAsync(TestContext.Current.CancellationToken);

            var published = Assert.Single(relayChannel.Published);
            Assert.Equal("order.created", published.Type);
        }

        [Fact]
        public async Task Processor_OnePendingMessage_MarksMessageAsDelivered()
        {
            var (provider, repository, _) = BuildProviderWithRelay();

            var msg = new FakeOutboxMessage(MakeEvent());
            repository.SeedAsync(msg);

            var processor = provider.GetRequiredService<IOutboxRelayProcessor>();
            await processor.ProcessPendingMessagesAsync(TestContext.Current.CancellationToken);

            Assert.Equal(OutboxMessageStatus.Delivered, msg.Status);
        }

        [Fact]
        public async Task Processor_MessageDeferredInFuture_IsNotPublished()
        {
            var (provider, repository, relayChannel) = BuildProviderWithRelay();

            var msg = new FakeOutboxMessage(MakeEvent("event.deferred"))
            {
                NextRetryAt = DateTimeOffset.UtcNow.AddMinutes(2)
            };
            repository.SeedAsync(msg);

            var processor = provider.GetRequiredService<IOutboxRelayProcessor>();
            await processor.ProcessPendingMessagesAsync(TestContext.Current.CancellationToken);

            Assert.Empty(relayChannel.Published);
            Assert.Equal(OutboxMessageStatus.Pending, msg.Status);
        }

        [Fact]
        public async Task Processor_MultiplePendingMessages_PublishesAllToRelayChannel()
        {
            var (provider, repository, relayChannel) = BuildProviderWithRelay();

            repository.SeedAsync(
                new FakeOutboxMessage(MakeEvent("event.one")),
                new FakeOutboxMessage(MakeEvent("event.two")),
                new FakeOutboxMessage(MakeEvent("event.three")));

            var processor = provider.GetRequiredService<IOutboxRelayProcessor>();
            await processor.ProcessPendingMessagesAsync(TestContext.Current.CancellationToken);

            Assert.Equal(3, relayChannel.Published.Count);
        }

        [Fact]
        public async Task Processor_MultiplePendingMessages_AllMarkedAsDelivered()
        {
            var (provider, repository, _) = BuildProviderWithRelay();

            var messages = new[]
            {
                new FakeOutboxMessage(MakeEvent("event.A")),
                new FakeOutboxMessage(MakeEvent("event.B")),
            };
            repository.SeedAsync(messages);

            var processor = provider.GetRequiredService<IOutboxRelayProcessor>();
            await processor.ProcessPendingMessagesAsync(TestContext.Current.CancellationToken);

            Assert.All(messages, m => Assert.Equal(OutboxMessageStatus.Delivered, m.Status));
        }

        [Fact]
        public async Task Processor_ChannelThrows_MessageMarkedAsFailed()
        {
            var repository   = new FakeOutboxMessageRepository();
            var factory      = new FakeOutboxMessageFactory();
            var failChannel  = new ThrowingRelayChannel();

            var services = new ServiceCollection();
            services.AddEventPublisher()
                    .AddOutbox<FakeOutboxMessage>()
                    .WithFactory<FakeOutboxMessageFactory>()
                    .WithRepository<FakeOutboxMessageRepository>()
                    .WithRelay(opts => opts.Interval = TimeSpan.FromMilliseconds(50))
                    .Services
                    .AddEventPublisher()
                    .AddChannel(failChannel);

            services.AddSingleton<IOutboxMessageFactory<FakeOutboxMessage>>(factory);
            services.AddSingleton<IOutboxMessageRepository<FakeOutboxMessage>>(repository);

            var provider = services.BuildServiceProvider();

            var msg = new FakeOutboxMessage(MakeEvent("failing.event"));
            repository.SeedAsync(msg);

            var processor = provider.GetRequiredService<IOutboxRelayProcessor>();
            await processor.ProcessPendingMessagesAsync(TestContext.Current.CancellationToken);

            Assert.Equal(OutboxMessageStatus.Failed, msg.Status);
            Assert.NotNull(msg.ErrorMessage);
        }

        [Fact]
        public async Task Processor_ChannelThrows_OtherMessagesStillProcessed()
        {
            var repository    = new FakeOutboxMessageRepository();
            var factory       = new FakeOutboxMessageFactory();
            var failChannel   = new ThrowingRelayChannel();

            var services = new ServiceCollection();
            services.AddEventPublisher()
                    .AddOutbox<FakeOutboxMessage>()
                    .WithFactory<FakeOutboxMessageFactory>()
                    .WithRepository<FakeOutboxMessageRepository>()
                    .WithRelay(opts => opts.Interval = TimeSpan.FromMilliseconds(50))
                    .Services
                    .AddEventPublisher()
                    .AddChannel(failChannel);

            services.AddSingleton<IOutboxMessageFactory<FakeOutboxMessage>>(factory);
            services.AddSingleton<IOutboxMessageRepository<FakeOutboxMessage>>(repository);

            var provider = services.BuildServiceProvider();

            var m1 = new FakeOutboxMessage(MakeEvent("first"));
            var m2 = new FakeOutboxMessage(MakeEvent("second"));
            repository.SeedAsync(m1, m2);

            var processor = provider.GetRequiredService<IOutboxRelayProcessor>();
            await processor.ProcessPendingMessagesAsync(TestContext.Current.CancellationToken);

            // Both messages should have been attempted; both should be failed because all
            // channel calls throw.
            Assert.Equal(OutboxMessageStatus.Failed, m1.Status);
            Assert.Equal(OutboxMessageStatus.Failed, m2.Status);
        }

        // ── MaxBatchSize ──────────────────────────────────────────────────────

        [Fact]
        public async Task Processor_MaxBatchSize_OnlyProcessesBatchMessages()
        {
            var (provider, repository, relayChannel) = BuildProviderWithRelay(opts =>
                opts.MaxBatchSize = 2);

            repository.SeedAsync(
                new FakeOutboxMessage(MakeEvent("event.1")),
                new FakeOutboxMessage(MakeEvent("event.2")),
                new FakeOutboxMessage(MakeEvent("event.3")));

            var processor = provider.GetRequiredService<IOutboxRelayProcessor>();
            await processor.ProcessPendingMessagesAsync(TestContext.Current.CancellationToken);

            // Only 2 of the 3 messages should have been forwarded.
            Assert.Equal(2, relayChannel.Published.Count);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static async Task PollAssertAsync(Func<bool> predicate, TimeSpan timeout, TimeSpan pollingInterval)
        {
            var sw = Stopwatch.StartNew();
            while (true)
            {
                if (predicate())
                    return;

                if (sw.Elapsed >= timeout)
                    throw new TimeoutException($"Condition not met within {timeout}");

                await Task.Delay(pollingInterval);
            }
        }

        // ── OutboxRelayService (integration via IHost) ────────────────────────

        [Fact]
        public async Task RelayService_AfterOneTick_DeliversPendingMessages()
        {
            var repository   = new FakeOutboxMessageRepository();
            var factory      = new FakeOutboxMessageFactory();
            var relayChannel = new FakeRelayChannel();

            var builder = Host.CreateDefaultBuilder()
                .ConfigureServices(services =>
                {
                    services.AddEventPublisher()
                            .AddOutbox<FakeOutboxMessage>()
                            .WithFactory<FakeOutboxMessageFactory>()
                            .WithRepository<FakeOutboxMessageRepository>()
                            .WithRelay(opts => opts.Interval = TimeSpan.FromMilliseconds(50))
                            .Services
                            .AddEventPublisher()
                            .AddChannel(relayChannel);

                    services.AddSingleton<IOutboxMessageFactory<FakeOutboxMessage>>(factory);
                    services.AddSingleton<IOutboxMessageRepository<FakeOutboxMessage>>(repository);
                });

            using var host = builder.Build();

            // Seed one pending message BEFORE starting the host.
            var msg = new FakeOutboxMessage(MakeEvent("hosted.event"));
            repository.SeedAsync(msg);

            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            await host.StartAsync(cts.Token);

            // Poll for the message to be delivered by the relay processor
            await PollAssertAsync(
                () => msg.Status == OutboxMessageStatus.Delivered && relayChannel.Published.Count == 1,
                timeout: TimeSpan.FromSeconds(5),
                pollingInterval: TimeSpan.FromMilliseconds(50));

            await host.StopAsync(TestContext.Current.CancellationToken);

            Assert.Equal(OutboxMessageStatus.Delivered, msg.Status);
            Assert.Single(relayChannel.Published);
        }

        // ── WithRelay registration tests ──────────────────────────────────────

        [Fact]
        public static void WithRelay_RegistersHostedService()
        {
            var services = new ServiceCollection();
            services.AddEventPublisher()
                    .AddOutbox<FakeOutboxMessage>()
                    .WithFactory<FakeOutboxMessageFactory>(ServiceLifetime.Singleton)
                    .WithRepository<FakeOutboxMessageRepository>(ServiceLifetime.Singleton)
                    .WithRelay();
            
            var provider = services.BuildServiceProvider();
            var hostedServices = provider.GetServices<IHostedService>().ToList();

            Assert.Contains(hostedServices, s => s is OutboxRelayServiceBase);
        }

        [Fact]
        public static void WithRelay_WithConfigureAction_OptionsAreApplied()
        {
            var services = new ServiceCollection();
            services.AddEventPublisher()
                    .AddOutbox<FakeOutboxMessage>()
                    .WithFactory<FakeOutboxMessageFactory>()
                    .WithRepository<FakeOutboxMessageRepository>()
                    .WithRelay(opts => opts.Interval = TimeSpan.FromSeconds(60));

            services.AddSingleton<IOutboxMessageFactory<FakeOutboxMessage>, FakeOutboxMessageFactory>();
            services.AddSingleton<IOutboxMessageRepository<FakeOutboxMessage>, FakeOutboxMessageRepository>();

            var provider = services.BuildServiceProvider();
            var opts = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<OutboxRelayOptions>>();

            Assert.Equal(TimeSpan.FromSeconds(60), opts.Value.Interval);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private sealed class ThrowingRelayChannel : IEventPublishChannel
        {
            public Task PublishAsync(CloudEvent @event, EventPublishOptions? options = null,
                CancellationToken cancellationToken = default)
                => throw new InvalidOperationException("Transport channel is deliberately broken.");
        }
    }
}















