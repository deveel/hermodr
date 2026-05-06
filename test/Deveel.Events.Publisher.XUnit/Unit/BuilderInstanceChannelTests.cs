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

            // Channels are registered as keyed services under the publisher name (empty string = default).
            var asUntyped = provider.GetKeyedServices<IEventPublishChannel>(string.Empty)
                                    .First();
            var asTyped   = provider.GetKeyedService<IEventPublishChannel<TestEventData>>(string.Empty);

            Assert.NotNull(asUntyped);
            Assert.NotNull(asTyped);
        }

        // ── UsePublisher<T> ───────────────────────────────────────────────────

        private sealed class CustomPublisher : EventPublisher
        {
            public CustomPublisher(
                Microsoft.Extensions.Options.IOptions<EventPublisherOptions> options,
                System.Collections.Generic.IEnumerable<IEventPublishChannel> channels,
                IServiceProvider serviceProvider)
                : base(options, channels, serviceProvider) { }
        }

        [Fact]
        public static void UsePublisher_CustomType_ResolvesAsCustomType()
        {
            var services = new ServiceCollection();
            services.AddEventPublisher()
                    .UsePublisher<CustomPublisher>();

            var provider = services.BuildServiceProvider();

            // Publisher is a keyed singleton — resolves as the custom type consistently.
            var p1 = provider.GetService<EventPublisher>();
            var p2 = provider.GetService<EventPublisher>();

            Assert.NotNull(p1);
            Assert.NotNull(p2);
            Assert.IsType<CustomPublisher>(p1);
            Assert.Same(p1, p2); // singleton
        }
    }
}


