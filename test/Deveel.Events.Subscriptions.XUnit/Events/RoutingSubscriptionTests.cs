// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using CloudNative.CloudEvents;

using Deveel.Filters;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Deveel.Events
{
    /// <summary>
    /// Tests for <see cref="RoutingEventSubscription"/> and the
    /// <c>RouteToChannel</c> builder extension methods.
    /// </summary>
    [Trait("Feature", "Subscriptions")]
    [Trait("Concern", "RoutingSubscription")]
    public class RoutingSubscriptionTests
    {
        // ── helpers ────────────────────────────────────────────────────────────

        private static CloudEvent MakeEvent(string type = "com.example.test") => new()
        {
            Type   = type,
            Source = new Uri("https://example.com"),
            Id     = Guid.NewGuid().ToString("N"),
            Time   = DateTimeOffset.UtcNow
        };

        /// <summary>EventPublisher subclass that captures the raw call to PublishEventAsync.</summary>
        private sealed class RecordingPublisher : EventPublisher
        {
            public List<CloudEvent> Published { get; } = new();
            public EventPublishOptions? LastOptions { get; private set; }

            public RecordingPublisher(
                IOptions<EventPublisherOptions> options,
                IEnumerable<IEventPublishChannel> channels,
                IServiceProvider serviceProvider)
                : base(options, channels, serviceProvider) { }

            public override Task PublishEventAsync(CloudEvent @event, EventPublishOptions? options = null, CancellationToken cancellationToken = default)
            {
                Published.Add(@event);
                LastOptions = options;
                return Task.CompletedTask;
            }
        }

        /// <summary>Builds a service provider with a <see cref="RecordingPublisher"/> registered.</summary>
        private static (IServiceProvider Provider, RecordingPublisher Recording) BuildRecordingProvider()
        {
            var services = new ServiceCollection();
            services.AddEventPublisher().UsePublisher<RecordingPublisher>();
            var provider = services.BuildServiceProvider();
            var recording = (RecordingPublisher)provider.GetRequiredService<EventPublisher>();
            return (provider, recording);
        }

        // ── RoutingEventSubscription constructor null-guards ───────────────────

        [Fact]
        public void Constructor_NullFilter_Throws()
        {
            var services = new ServiceCollection().BuildServiceProvider();

            Assert.Throws<ArgumentNullException>(() =>
                new RoutingEventSubscription(null!, services));
        }

        [Fact]
        public void Constructor_NullServices_Throws()
        {
            var filter = FilterExpression.Constant(true);

            Assert.Throws<ArgumentNullException>(() =>
                new RoutingEventSubscription(filter, null!));
        }

        [Fact]
        public void Constructor_SetsProperties()
        {
            var filter  = FilterExpression.Constant(true);
            var options = new NamedChannelPublishOptions("ch");
            var services = new ServiceCollection().BuildServiceProvider();

            var sub = new RoutingEventSubscription(filter, services, options, "my-route");

            Assert.Equal("my-route", sub.Name);
            Assert.Same(filter, sub.Filter);
            Assert.Same(options, sub.RoutingOptions);
        }

        // ── RoutingEventSubscription.HandleAsync ───────────────────────────────

        [Fact]
        public async Task HandleAsync_RepublishesEventThroughPublisher()
        {
            var (sp, recording) = BuildRecordingProvider();

            var filter = FilterExpression.Constant(true);
            var sub    = new RoutingEventSubscription(filter, sp);

            var @event = MakeEvent();
            await sub.HandleAsync(@event);

            Assert.Single(recording.Published);
            Assert.Same(@event, recording.Published[0]);
            Assert.Null(recording.LastOptions);
        }

        [Fact]
        public async Task HandleAsync_ForwardsRoutingOptions()
        {
            var opts = new NamedChannelPublishOptions("target-channel");

            var (sp, recording) = BuildRecordingProvider();

            var sub = new RoutingEventSubscription(FilterExpression.Constant(true), sp, opts);

            var @event = MakeEvent();
            await sub.HandleAsync(@event);

            Assert.Same(opts, recording.LastOptions);
        }

        // ── RouteToChannel(FilterExpression, …) builder extension ─────────────

        [Fact]
        public static void RouteToChannel_Filter_RegistersRoutingSubscription_InDI()
        {
            // Verifies that RouteToChannel registers a RoutingEventSubscription in DI
            // without triggering circular dispatch.
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventPublisher(o => o.Source = new Uri("https://example.com"))
                    .AddSubscriptions()
                    .RouteToChannel(
                        EventFilter.ByType("com.example.route"),
                        name: "test-route");

            var provider = services.BuildServiceProvider();

            // The subscription should be registered as IEventSubscription
            var subscriptions = provider.GetServices<IEventSubscription>().ToList();
            var routingSubscription = subscriptions.OfType<RoutingEventSubscription>().FirstOrDefault();

            Assert.NotNull(routingSubscription);
            Assert.Equal("test-route", routingSubscription!.Name);
        }

        [Fact]
        public async Task RouteToChannel_Filter_RoutingSubscription_RepublishesMatchedEvent()
        {
            // Test the routing end-to-end using a RecordingPublisher so there's no
            // infinite loop: the routing subscription re-publishes via the recording
            // publisher, which simply records and does not loop back.
            var (sp, recording) = BuildRecordingProvider();

            var filter = EventFilter.ByType("com.example.route");
            var sub = new RoutingEventSubscription(filter, sp, null, "route-sub");

            var matchingEvent = MakeEvent("com.example.route");
            await sub.HandleAsync(matchingEvent);

            Assert.Single(recording.Published);
            Assert.Same(matchingEvent, recording.Published[0]);
        }

        [Fact]
        public static async Task RouteToChannel_Filter_NonMatchingEvent_SubscriptionNotInvoked()
        {
            // Verify the routing subscription is not selected when the filter does not match.
            var (sp, recording) = BuildRecordingProvider();

            var filter = EventFilter.ByType("com.example.specific");
            var sub = new RoutingEventSubscription(filter, sp);

            var registry = new EventSubscriptionRegistry([sub]);
            var matches = await registry.ResolveSubscriptionsAsync(MakeEvent("com.example.other"));

            Assert.Empty(matches);
            Assert.Empty(recording.Published);
        }

        // ── RouteToChannel(string typePattern, …) builder extension ───────────

        [Fact]
        public static void RouteToChannel_TypePattern_RegistersRoutingSubscription_InDI()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventPublisher(o => o.Source = new Uri("https://example.com"))
                    .AddSubscriptions()
                    .RouteToChannel("com.example.*", name: "pattern-route");

            var provider = services.BuildServiceProvider();

            var subscriptions = provider.GetServices<IEventSubscription>().ToList();
            var routingSubscription = subscriptions.OfType<RoutingEventSubscription>().FirstOrDefault();

            Assert.NotNull(routingSubscription);
            Assert.Equal("pattern-route", routingSubscription!.Name);
        }

        [Fact]
        public async Task RouteToChannel_TypePattern_WithRoutingOptions_ForwardsOptions()
        {
            var opts = new NamedChannelPublishOptions("output-channel");

            var (sp, recording) = BuildRecordingProvider();

            var sub = new RoutingEventSubscription(
                EventFilter.ByTypePattern("com.example.*"),
                sp,
                opts,
                "pattern-sub");

            await sub.HandleAsync(MakeEvent("com.example.order"));

            Assert.Single(recording.Published);
            Assert.Same(opts, recording.LastOptions);
        }

        [Fact]
        public static void RouteToChannel_TypePattern_NullOptions_SetsNullRoutingOptions()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventPublisher(o => o.Source = new Uri("https://example.com"))
                    .AddSubscriptions()
                    .RouteToChannel("com.example.test", routingOptions: null);

            var provider = services.BuildServiceProvider();

            var sub = provider.GetServices<IEventSubscription>()
                              .OfType<RoutingEventSubscription>()
                              .FirstOrDefault();

            Assert.NotNull(sub);
            Assert.Null(sub!.RoutingOptions);
        }
    }
}

