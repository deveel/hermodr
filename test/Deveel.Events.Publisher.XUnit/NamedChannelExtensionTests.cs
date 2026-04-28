// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using CloudNative.CloudEvents;

using Microsoft.Extensions.DependencyInjection;

namespace Deveel.Events
{
    /// <summary>
    /// Tests for the named-channel convenience extension methods on
    /// <see cref="IEventPublisher"/>:
    ///   - <c>PublishAsync{TEvent}(publisher, event, channelName, ct)</c>
    ///   - <c>PublishEventAsync(publisher, event, channelName, ct)</c>
    /// </summary>
    [Trait("Function", "Publisher")]
    [Trait("Concern", "NamedChannelExtensions")]
    public class NamedChannelExtensionTests
    {
        // ── helpers ────────────────────────────────────────────────────────────

        [Event("order.placed", "https://example.com/events/order.placed/1.0")]
        private sealed class OrderPlaced
        {
            public string OrderId { get; set; } = string.Empty;
        }

        /// <summary>
        /// A minimal named publish channel that records events it receives.
        /// </summary>
        private sealed class NamedCallbackChannel : INamedEventPublishChannel
        {
            private readonly Action<CloudEvent> _callback;

            public NamedCallbackChannel(string name, Action<CloudEvent> callback)
            {
                Name = name;
                _callback = callback;
            }

            public string? Name { get; }

            public Task PublishAsync(
                CloudEvent @event,
                EventPublishOptions? options = null,
                CancellationToken cancellationToken = default)
            {
                _callback(@event);
                return Task.CompletedTask;
            }
        }

        /// <summary>An unnamed channel (no <see cref="INamedEventPublishChannel"/>).</summary>
        private sealed class AnonymousCallbackChannel : IEventPublishChannel
        {
            private readonly Action<CloudEvent> _callback;

            public AnonymousCallbackChannel(Action<CloudEvent> callback)
                => _callback = callback;

            public Task PublishAsync(
                CloudEvent @event,
                EventPublishOptions? options = null,
                CancellationToken cancellationToken = default)
            {
                _callback(@event);
                return Task.CompletedTask;
            }
        }

        private static IEventPublisher BuildPublisher(params IEventPublishChannel[] channels)
        {
            var services = new ServiceCollection();
            services.AddEventPublisher(o =>
            {
                o.Source = new Uri("https://api.example.com");
                o.DataSchemaBaseUri = new Uri("https://schemas.example.com/events");
            });

            foreach (var ch in channels)
                services.AddSingleton<IEventPublishChannel>(ch);

            return services.BuildServiceProvider().GetRequiredService<IEventPublisher>();
        }

        private static CloudEvent ValidEvent(string type = "test.event") => new()
        {
            Type = type,
            Source = new Uri("https://example.com"),
            Id = Guid.NewGuid().ToString("N"),
        };

        // ── PublishAsync<TEvent>(string channelName) ──────────────────────────

        [Fact]
        public async Task PublishAsync_Generic_WithChannelName_OnlyTargetChannelReceivesEvent()
        {
            var targetEvents = new List<CloudEvent>();
            var otherEvents  = new List<CloudEvent>();

            var publisher = BuildPublisher(
                new NamedCallbackChannel("target", e => targetEvents.Add(e)),
                new NamedCallbackChannel("other",  e => otherEvents.Add(e)));

            await publisher.PublishAsync(new OrderPlaced { OrderId = "42" },
                "target", TestContext.Current.CancellationToken);

            Assert.Single(targetEvents);
            Assert.Empty(otherEvents);
        }

        [Fact]
        public async Task PublishAsync_Generic_WithChannelName_AnonymousChannelStillReceivesEvent()
        {
            var namedEvents     = new List<CloudEvent>();
            var anonymousEvents = new List<CloudEvent>();

            var publisher = BuildPublisher(
                new NamedCallbackChannel("named",    e => namedEvents.Add(e)),
                new AnonymousCallbackChannel(        e => anonymousEvents.Add(e)));

            await publisher.PublishAsync(new OrderPlaced { OrderId = "7" },
                "named", TestContext.Current.CancellationToken);

            // Anonymous channels have no name filter → they receive everything
            Assert.Single(namedEvents);
            Assert.Single(anonymousEvents);
        }

        [Fact]
        public async Task PublishAsync_Generic_WithChannelName_NonMatchingNamedChannelSkipped()
        {
            var alphaEvents = new List<CloudEvent>();
            var betaEvents  = new List<CloudEvent>();

            var publisher = BuildPublisher(
                new NamedCallbackChannel("alpha", e => alphaEvents.Add(e)),
                new NamedCallbackChannel("beta",  e => betaEvents.Add(e)));

            await publisher.PublishAsync(new OrderPlaced { OrderId = "3" },
                "alpha", TestContext.Current.CancellationToken);

            Assert.Single(alphaEvents);
            Assert.Empty(betaEvents);
        }

        // ── PublishEventAsync(CloudEvent, string channelName) ─────────────────

        [Fact]
        public async Task PublishEventAsync_WithChannelName_TargetChannelReceivesCloudEvent()
        {
            var targetEvents = new List<CloudEvent>();
            var otherEvents  = new List<CloudEvent>();

            var publisher = BuildPublisher(
                new NamedCallbackChannel("target", e => targetEvents.Add(e)),
                new NamedCallbackChannel("other",  e => otherEvents.Add(e)));

            await publisher.PublishEventAsync(ValidEvent(), "target",
                TestContext.Current.CancellationToken);

            Assert.Single(targetEvents);
            Assert.Empty(otherEvents);
        }

        [Fact]
        public async Task PublishEventAsync_WithChannelName_CaseInsensitiveMatch()
        {
            var events = new List<CloudEvent>();

            var publisher = BuildPublisher(
                new NamedCallbackChannel("MyChannel", e => events.Add(e)));

            // Channel name is "MyChannel" but filter name is "mychannel" — should still match
            await publisher.PublishEventAsync(ValidEvent(), "mychannel",
                TestContext.Current.CancellationToken);

            Assert.Single(events);
        }

        [Fact]
        public async Task PublishEventAsync_WithChannelName_AnonymousChannelReceivesEvent()
        {
            var namedEvents     = new List<CloudEvent>();
            var anonymousEvents = new List<CloudEvent>();

            var publisher = BuildPublisher(
                new NamedCallbackChannel("chan",  e => namedEvents.Add(e)),
                new AnonymousCallbackChannel(    e => anonymousEvents.Add(e)));

            await publisher.PublishEventAsync(ValidEvent(), "chan",
                TestContext.Current.CancellationToken);

            Assert.Single(namedEvents);
            Assert.Single(anonymousEvents);
        }

        [Fact]
        public async Task PublishEventAsync_WithChannelName_EmptyNamedChannelTreatedAsAnonymous()
        {
            // A channel that implements INamedEventPublishChannel but has an empty name
            // is treated as unnamed and should receive events regardless of the filter.
            var events = new List<CloudEvent>();

            var publisher = BuildPublisher(
                new NamedCallbackChannel("", e => events.Add(e)));

            await publisher.PublishEventAsync(ValidEvent(), "anything",
                TestContext.Current.CancellationToken);

            Assert.Single(events);
        }

        // ── NamedChannelPublishOptions default constructor ────────────────────

        [Fact]
        public void NamedChannelPublishOptions_DefaultCtor_ChannelNameIsNull()
        {
            var opts = new NamedChannelPublishOptions();
            Assert.Null(opts.ChannelName);
        }

        [Fact]
        public void NamedChannelPublishOptions_NamedCtor_ChannelNameIsSet()
        {
            var opts = new NamedChannelPublishOptions("my-channel");
            Assert.Equal("my-channel", opts.ChannelName);
        }
    }
}

