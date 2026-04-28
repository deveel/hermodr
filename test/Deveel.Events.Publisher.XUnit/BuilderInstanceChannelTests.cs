// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using CloudNative.CloudEvents;

using Microsoft.Extensions.DependencyInjection;

namespace Deveel.Events
{
    /// <summary>
    /// Tests that fill coverage gaps in <see cref="EventPublisherBuilder"/>:
    ///   - <c>AddChannel(IEventPublishChannel instance)</c>
    ///   - <c>AddChannel{TEvent}(IEventPublishChannel{TEvent} instance)</c>
    ///   - <c>UsePublisher{T}</c> when T does not derive from <see cref="EventPublisher"/>
    ///   - <c>UsePublisher{T}</c> with <see cref="ServiceLifetime.Transient"/>
    /// </summary>
    [Trait("Function", "Registration")]
    [Trait("Concern", "BuilderInstanceChannels")]
    public static class BuilderInstanceChannelTests
    {
        // ── helpers ────────────────────────────────────────────────────────────

        [Event("test.instance", "https://example.com/events/test/1.0")]
        private class TestEventData { }

        private sealed class CallbackChannel : IEventPublishChannel
        {
            private readonly Action<CloudEvent> _callback;
            public CallbackChannel(Action<CloudEvent> cb) => _callback = cb;

            public Task PublishAsync(
                CloudEvent @event,
                EventPublishOptions? options = null,
                CancellationToken cancellationToken = default)
            {
                _callback(@event);
                return Task.CompletedTask;
            }
        }

        private sealed class TypedCallbackChannel<TEvent> :
            IEventPublishChannel, IEventPublishChannel<TEvent>
            where TEvent : class
        {
            private readonly Action<CloudEvent> _callback;
            public TypedCallbackChannel(Action<CloudEvent> cb) => _callback = cb;

            public Task PublishAsync(
                CloudEvent @event,
                EventPublishOptions? options = null,
                CancellationToken cancellationToken = default)
            {
                _callback(@event);
                return Task.CompletedTask;
            }
        }

        private static CloudEvent ValidEvent() => new()
        {
            Type   = "test.event",
            Source = new Uri("https://example.com"),
            Id     = Guid.NewGuid().ToString("N"),
        };

        // ── AddChannel(IEventPublishChannel instance) ─────────────────────────

        [Fact]
        public static async Task AddChannel_Instance_EventDelivered()
        {
            var received = new List<CloudEvent>();
            var channel  = new CallbackChannel(e => received.Add(e));

            var services = new ServiceCollection();
            services.AddEventPublisher().AddChannel(channel);

            var publisher = services.BuildServiceProvider()
                                    .GetRequiredService<EventPublisher>();

            await publisher.PublishEventAsync(ValidEvent());

            Assert.Single(received);
        }

        [Fact]
        public static async Task AddChannel_Instance_MultipleInstancesAllReceive()
        {
            var ch1Events = new List<CloudEvent>();
            var ch2Events = new List<CloudEvent>();

            var services = new ServiceCollection();
            services.AddEventPublisher()
                    .AddChannel(new CallbackChannel(e => ch1Events.Add(e)))
                    .AddChannel(new CallbackChannel(e => ch2Events.Add(e)));

            var publisher = services.BuildServiceProvider()
                                    .GetRequiredService<EventPublisher>();

            await publisher.PublishEventAsync(ValidEvent());

            Assert.Single(ch1Events);
            Assert.Single(ch2Events);
        }

        // ── AddChannel<TEvent>(IEventPublishChannel<TEvent> instance) ─────────

        [Fact]
        public static async Task AddChannel_TypedInstance_EventDelivered()
        {
            var received = new List<CloudEvent>();
            var channel  = new TypedCallbackChannel<TestEventData>(e => received.Add(e));

            var services = new ServiceCollection();
            services.AddEventPublisher(o =>
            {
                o.Source = new Uri("https://api.example.com");
                o.DataSchemaBaseUri = new Uri("https://schemas.example.com/events");
            }).AddChannel<TestEventData>(channel);

            var publisher = services.BuildServiceProvider()
                                    .GetRequiredService<EventPublisher>();

            await publisher.PublishAsync(typeof(TestEventData), new TestEventData());

            Assert.Single(received);
        }

        [Fact]
        public static void AddChannel_TypedInstance_RegisteredAsBothInterfaces()
        {
            var channel = new TypedCallbackChannel<TestEventData>(_ => { });

            var services = new ServiceCollection();
            services.AddEventPublisher().AddChannel<TestEventData>(channel);

            var provider = services.BuildServiceProvider();

            // Both interface registrations should resolve (and point to the same instance)
            var asUntyped = provider.GetServices<IEventPublishChannel>().First(c => c is TypedCallbackChannel<TestEventData>);
            var asTyped   = provider.GetService<IEventPublishChannel<TestEventData>>();

            Assert.NotNull(asUntyped);
            Assert.NotNull(asTyped);
            Assert.Same(channel, asUntyped);
            Assert.Same(channel, asTyped);
        }

        // ── UsePublisher<T> when T does not extend EventPublisher ─────────────

        /// <summary>
        /// A publisher that implements <see cref="IEventPublisher"/> directly
        /// without inheriting from <see cref="EventPublisher"/>.
        /// </summary>
        private sealed class MinimalPublisher : IEventPublisher
        {
            public Task PublishAsync(Type eventType, object? @event, EventPublishOptions? options = null, CancellationToken cancellationToken = default)
                => Task.CompletedTask;

            public Task PublishAsync<TData>(TData data, EventPublishOptions? options = null, CancellationToken cancellationToken = default)
                => Task.CompletedTask;

            public Task PublishEventAsync(CloudEvent @event, EventPublishOptions? options = null, CancellationToken cancellationToken = default)
                => Task.CompletedTask;
        }

        [Fact]
        public static void UsePublisher_NonEventPublisherSubclass_DoesNotRegisterEventPublisherAlias()
        {
            var services = new ServiceCollection();
            services.AddEventPublisher()
                    .UsePublisher<MinimalPublisher>();

            var provider = services.BuildServiceProvider();

            // IEventPublisher should resolve to our custom type
            var publisher = provider.GetService<IEventPublisher>();
            Assert.IsType<MinimalPublisher>(publisher);

            // EventPublisher (the base class alias) should NOT resolve to our type —
            // the UsePublisher<T> branch only registers the EventPublisher alias when
            // T actually derives from EventPublisher.
            var basePublisher = provider.GetService<EventPublisher>();
            // It may still be registered as the default singleton from AddDefaultServices;
            // what matters is it is NOT MinimalPublisher.
            if (basePublisher != null)
                Assert.IsNotType<MinimalPublisher>(basePublisher);
        }

        // ── UsePublisher<T> with Transient lifetime ───────────────────────────

        private sealed class TransientPublisher : EventPublisher
        {
            public TransientPublisher(
                Microsoft.Extensions.Options.IOptions<EventPublisherOptions> options,
                System.Collections.Generic.IEnumerable<IEventPublishChannel> channels,
                IEventCreator? eventCreator = null,
                IEventIdGenerator? idGenerator = null,
                IEventSystemTime? systemTime = null)
                : base(options, channels, eventCreator, idGenerator, systemTime) { }
        }

        [Fact]
        public static void UsePublisher_TransientLifetime_ResolvesToNewInstanceEachTime()
        {
            var services = new ServiceCollection();
            services.AddEventPublisher()
                    .UsePublisher<TransientPublisher>(ServiceLifetime.Transient);

            var provider = services.BuildServiceProvider();

            // Resolving twice should yield different instances for Transient
            var p1 = provider.GetService<IEventPublisher>();
            var p2 = provider.GetService<IEventPublisher>();

            Assert.NotNull(p1);
            Assert.NotNull(p2);
            Assert.IsType<TransientPublisher>(p1);
            Assert.IsType<TransientPublisher>(p2);
            Assert.NotSame(p1, p2);
        }
    }
}


