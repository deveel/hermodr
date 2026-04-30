//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

using Deveel.Events.Fakes;

using Microsoft.Extensions.DependencyInjection;

namespace Deveel.Events
{
    /// <summary>
    /// Tests that verify the <see cref="OutboxPublishChannel{TMessage}"/> persists
    /// events correctly through the factory/repository pair.
    /// </summary>
    [Trait("Component", "OutboxPublishChannel")]
    public class OutboxPublishChannelTests
    {
        // ── Helpers ──────────────────────────────────────────────────────────

        private static CloudEvent MakeEvent(string type = "test.event") => new()
        {
            Type   = type,
            Source = new Uri("https://example.com"),
            Id     = Guid.NewGuid().ToString("N"),
        };

        /// <summary>
        /// Builds a minimal <see cref="IServiceProvider"/> that can resolve an
        /// <see cref="OutboxPublishChannel{TMessage}"/> wired to the given fakes.
        /// </summary>
        private static IServiceProvider BuildProvider(
            FakeOutboxMessageFactory factory,
            FakeOutboxMessageRepository repository)
        {
            var services = new ServiceCollection();

            services.AddEventPublisher()
                    .AddOutbox<FakeOutboxMessage>()
                    .WithFactory<FakeOutboxMessageFactory>()
                    .WithRepository<FakeOutboxMessageRepository>();

            // Replace registrations with pre-built instances so tests share the
            // same objects and can inspect them afterward.
            services.AddSingleton<IOutboxMessageFactory<FakeOutboxMessage>>(factory);
            services.AddSingleton<IOutboxMessageRepository<FakeOutboxMessage>>(repository);

            return services.BuildServiceProvider();
        }

        // ── PublishAsync: happy path ────────────────────────────────────────

        [Fact]
        public async Task PublishAsync_ValidEvent_CreatesMessageViaFactory()
        {
            var factory    = new FakeOutboxMessageFactory();
            var repository = new FakeOutboxMessageRepository();
            var provider   = BuildProvider(factory, repository);

            var publisher = provider.GetRequiredService<EventPublisher>();
            var evt       = MakeEvent("order.placed");

            await publisher.PublishEventAsync(evt, cancellationToken: TestContext.Current.CancellationToken);

            var created = Assert.Single(factory.Created);
            Assert.Equal("order.placed", created.CloudEvent.Type);
        }

        [Fact]
        public async Task PublishAsync_ValidEvent_PersistsMessageInRepository()
        {
            var factory    = new FakeOutboxMessageFactory();
            var repository = new FakeOutboxMessageRepository();
            var provider   = BuildProvider(factory, repository);

            var publisher = provider.GetRequiredService<EventPublisher>();
            await publisher.PublishEventAsync(MakeEvent(), cancellationToken: TestContext.Current.CancellationToken);

            Assert.Single(repository.Store);
        }

        [Fact]
        public async Task PublishAsync_ValidEvent_StoredMessageHasPendingStatus()
        {
            var factory    = new FakeOutboxMessageFactory();
            var repository = new FakeOutboxMessageRepository();
            var provider   = BuildProvider(factory, repository);

            var publisher = provider.GetRequiredService<EventPublisher>();
            await publisher.PublishEventAsync(MakeEvent(), cancellationToken: TestContext.Current.CancellationToken);

            var stored = Assert.Single(repository.Store);
            Assert.Equal(OutboxMessageStatus.Pending, stored.Status);
        }

        [Fact]
        public async Task PublishAsync_MultipleEvents_AllPersistedInOrder()
        {
            var factory    = new FakeOutboxMessageFactory();
            var repository = new FakeOutboxMessageRepository();
            var provider   = BuildProvider(factory, repository);

            var publisher = provider.GetRequiredService<EventPublisher>();

            await publisher.PublishEventAsync(MakeEvent("event.one"),   cancellationToken: TestContext.Current.CancellationToken);
            await publisher.PublishEventAsync(MakeEvent("event.two"),   cancellationToken: TestContext.Current.CancellationToken);
            await publisher.PublishEventAsync(MakeEvent("event.three"), cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal(3, repository.Store.Count);
            Assert.Equal("event.one",   repository.Store[0].CloudEvent.Type);
            Assert.Equal("event.two",   repository.Store[1].CloudEvent.Type);
            Assert.Equal("event.three", repository.Store[2].CloudEvent.Type);
        }

        // ── PublishAsync: factory error ──────────────────────────────────────

        [Fact]
        public async Task PublishAsync_WhenFactoryThrows_ExceptionPropagates()
        {
            var repository = new FakeOutboxMessageRepository();
            var factory    = new ThrowingFactory();

            var services = new ServiceCollection();
            services.AddEventPublisher(o => o.ThrowOnErrors = true)
                    .AddOutbox<FakeOutboxMessage>()
                    .WithFactory<ThrowingFactory>()
                    .WithRepository<FakeOutboxMessageRepository>();

            services.AddSingleton<IOutboxMessageFactory<FakeOutboxMessage>>(factory);
            services.AddSingleton<IOutboxMessageRepository<FakeOutboxMessage>>(repository);

            var publisher = services.BuildServiceProvider().GetRequiredService<EventPublisher>();

            // When ThrowOnErrors = true the publisher wraps channel errors in EventPublishException.
            await Assert.ThrowsAsync<EventPublishException>(
                () => publisher.PublishEventAsync(MakeEvent(), cancellationToken: TestContext.Current.CancellationToken));

            Assert.Empty(repository.Store);
        }

        // ── PublishAsync: repository error ───────────────────────────────────

        [Fact]
        public async Task PublishAsync_WhenRepositoryThrows_ExceptionPropagates()
        {
            var factory    = new FakeOutboxMessageFactory();
            var repository = new ThrowingRepository();

            var services = new ServiceCollection();
            services.AddEventPublisher(o => o.ThrowOnErrors = true)
                    .AddOutbox<FakeOutboxMessage>()
                    .WithFactory<FakeOutboxMessageFactory>()
                    .WithRepository<ThrowingRepository>();

            services.AddSingleton<IOutboxMessageFactory<FakeOutboxMessage>>(factory);
            services.AddSingleton<IOutboxMessageRepository<FakeOutboxMessage>>(repository);

            var publisher = services.BuildServiceProvider().GetRequiredService<EventPublisher>();

            await Assert.ThrowsAsync<EventPublishException>(
                () => publisher.PublishEventAsync(MakeEvent(), cancellationToken: TestContext.Current.CancellationToken));
        }

        // ── Null-guard tests via DI resolution ───────────────────────────────
        // The channel is internal so we verify the guards indirectly: the DI
        // container will fail to activate the channel when a required dependency
        // is missing and throw InvalidOperationException during service resolution.

        [Fact]
        public void Resolve_WithoutFactory_ThrowsOnServiceResolution()
        {
            var services = new ServiceCollection();
            services.AddEventPublisher()
                    .AddOutbox<FakeOutboxMessage>()
                    // factory deliberately omitted
                    .WithRepository<FakeOutboxMessageRepository>();

            services.AddSingleton<IOutboxMessageRepository<FakeOutboxMessage>, FakeOutboxMessageRepository>();

            var provider = services.BuildServiceProvider();

            // The publisher is lazily resolved; acquiring it triggers channel activation.
            Assert.Throws<InvalidOperationException>(
                () => provider.GetRequiredService<EventPublisher>());
        }

        [Fact]
        public void Resolve_WithoutRepository_ThrowsOnServiceResolution()
        {
            var services = new ServiceCollection();
            services.AddEventPublisher()
                    .AddOutbox<FakeOutboxMessage>()
                    .WithFactory<FakeOutboxMessageFactory>();
                    // repository deliberately omitted

            var provider = services.BuildServiceProvider();

            Assert.Throws<InvalidOperationException>(
                () => provider.GetRequiredService<EventPublisher>());
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private sealed class ThrowingFactory : IOutboxMessageFactory<FakeOutboxMessage>
        {
            public FakeOutboxMessage Create(CloudEvent cloudEvent, OutboxPublishOptions? options = null)
                => throw new InvalidOperationException("Factory is deliberately broken.");
        }

        private sealed class ThrowingRepository : IOutboxMessageRepository<FakeOutboxMessage>
        {
            public string GetEntityKey(FakeOutboxMessage entity) => entity.Id;
            public Task AddAsync(FakeOutboxMessage entity, CancellationToken ct = default)
                => throw new InvalidOperationException("Repository is deliberately broken.");
            public Task AddRangeAsync(IEnumerable<FakeOutboxMessage> entities, CancellationToken ct = default)
                => throw new NotImplementedException();
            public Task<bool> UpdateAsync(FakeOutboxMessage entity, CancellationToken ct = default)
                => throw new NotImplementedException();
            public Task<bool> RemoveAsync(FakeOutboxMessage entity, CancellationToken ct = default)
                => throw new NotImplementedException();
            public Task RemoveRangeAsync(IEnumerable<FakeOutboxMessage> entities, CancellationToken ct = default)
                => throw new NotImplementedException();
            public Task<FakeOutboxMessage?> FindAsync(string key, CancellationToken ct = default)
                => throw new NotImplementedException();
            public Task SetSendingAsync(FakeOutboxMessage message, CancellationToken ct = default)
                => throw new NotImplementedException();
            public Task SetDeliveredAsync(FakeOutboxMessage message, CancellationToken ct = default)
                => throw new NotImplementedException();
            public Task SetRetryAsync(FakeOutboxMessage message, string errorMessage, DateTimeOffset nextRetryAt, CancellationToken ct = default)
                => throw new NotImplementedException();
            public Task SetFailedAsync(FakeOutboxMessage message, string errorMessage, CancellationToken ct = default)
                => throw new NotImplementedException();
            public Task<IReadOnlyList<FakeOutboxMessage>> GetPendingMessagesAsync(int? limit = null, CancellationToken ct = default)
                => throw new NotImplementedException();
        }
    }
}







