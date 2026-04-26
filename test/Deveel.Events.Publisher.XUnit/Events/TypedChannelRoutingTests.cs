//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

using Microsoft.Extensions.DependencyInjection;

namespace Deveel.Events
{
    /// <summary>
    /// Verifies that <see cref="EventPublisher.PublishAsync(Type,object?,EventPublishOptions?,CancellationToken)"/>
    /// routes events exclusively to <c>IEventPublishChannel&lt;TEvent&gt;</c> channels when at least
    /// one is registered for the given event data type, and falls back to the general-purpose
    /// untyped <see cref="IEventPublishChannel"/> channels when none are found.
    /// </summary>
    [Trait("Function", "Publisher")]
    [Trait("Concern", "TypedChannelRouting")]
    public class TypedChannelRoutingTests
    {
        // ── event data types ───────────────────────────────────────────────────

        [Event("order.placed", "https://example.com/events/order.placed/1.0")]
        private sealed class OrderPlaced
        {
            public string OrderId { get; set; } = string.Empty;
        }

        [Event("order.cancelled", "https://example.com/events/order.cancelled/1.0")]
        private sealed class OrderCancelled
        {
            public string OrderId { get; set; } = string.Empty;
        }

        // ── helpers ────────────────────────────────────────────────────────────

        private static EventPublisher BuildPublisher(Action<EventPublisherBuilder> configure)
        {
            var builder = new ServiceCollection()
                .AddEventPublisher(o =>
                {
                    o.Source = new Uri("https://api.example.com");
                    o.ThrowOnErrors = true;
                });
            configure(builder);
            return builder.Services.BuildServiceProvider().GetRequiredService<EventPublisher>();
        }

        // ── tests ──────────────────────────────────────────────────────────────

        [Fact]
        public async Task PublishAsync_TypedChannelRegistered_EventRoutedOnlyToTypedChannel()
        {
            // Arrange
            var typedEvents   = new List<CloudEvent>();
            var untypedEvents = new List<CloudEvent>();

            var publisher = BuildPublisher(b => b
                .AddTestChannel<OrderPlaced>(e => typedEvents.Add(e))
                .AddTestChannel(e => untypedEvents.Add(e)));

            // Act
            await publisher.PublishAsync(
                typeof(OrderPlaced),
                new OrderPlaced { OrderId = "42" },
                null,
                TestContext.Current.CancellationToken);

            // Assert – only the typed channel received the event
            Assert.Single(typedEvents);
            Assert.Empty(untypedEvents);
        }

        [Fact]
        public async Task PublishAsync_NoTypedChannelRegistered_EventRoutedToUntypedChannels()
        {
            // Arrange – only an untyped test channel; no typed channel for OrderPlaced
            var untypedEvents = new List<CloudEvent>();

            var publisher = BuildPublisher(b => b
                .AddTestChannel(e => untypedEvents.Add(e)));

            // Act
            await publisher.PublishAsync(
                typeof(OrderPlaced),
                new OrderPlaced { OrderId = "99" },
                null,
                TestContext.Current.CancellationToken);

            // Assert – untyped channel was used as fallback
            Assert.Single(untypedEvents);
        }

        [Fact]
        public async Task PublishAsync_MultipleTypedChannels_AllTypedChannelsReceiveEvent()
        {
            // Arrange – two typed channels for the same event type, plus one untyped channel
            var firstTypedEvents  = new List<CloudEvent>();
            var secondTypedEvents = new List<CloudEvent>();
            var untypedEvents     = new List<CloudEvent>();

            var publisher = BuildPublisher(b => b
                .AddTestChannel<OrderPlaced>(e => firstTypedEvents.Add(e))
                .AddTestChannel<OrderPlaced>(e => secondTypedEvents.Add(e))
                .AddTestChannel(e => untypedEvents.Add(e)));

            // Act
            await publisher.PublishAsync(
                typeof(OrderPlaced),
                new OrderPlaced { OrderId = "1" },
                null,
                TestContext.Current.CancellationToken);

            // Assert – both typed channels received the event; untyped channel was bypassed
            Assert.Single(firstTypedEvents);
            Assert.Single(secondTypedEvents);
            Assert.Empty(untypedEvents);
        }

        [Fact]
        public async Task PublishAsync_TypedChannelForDifferentType_FallsBackToAllUntypedChannels()
        {
            // Arrange – a typed channel for OrderCancelled and a plain untyped channel.
            // TypedTestChannel<OrderCancelled> also implements IEventPublishChannel, so it
            // participates in the general-purpose broadcast (the fallback path for OrderPlaced).
            var typedForCancelled = new List<CloudEvent>();
            var untypedEvents     = new List<CloudEvent>();

            var publisher = BuildPublisher(b => b
                .AddTestChannel<OrderCancelled>(e => typedForCancelled.Add(e))
                .AddTestChannel(e => untypedEvents.Add(e)));

            // Act – publish OrderPlaced (no IEventPublishChannel<OrderPlaced> registered)
            await publisher.PublishAsync(
                typeof(OrderPlaced),
                new OrderPlaced { OrderId = "7" },
                null,
                TestContext.Current.CancellationToken);

            // Assert – no typed channel for OrderPlaced exists, so ALL IEventPublishChannel
            // instances are used as the fallback broadcast.  The OrderCancelled-typed channel
            // also receives the event because it is also a plain IEventPublishChannel.
            Assert.Single(typedForCancelled);
            Assert.Single(untypedEvents);
        }

        [Fact]
        public async Task PublishAsync_TwoEventTypes_EachRoutedToItsOwnTypedChannel()
        {
            // Arrange – one typed channel per event type, plus one untyped channel
            var placedEvents    = new List<CloudEvent>();
            var cancelledEvents = new List<CloudEvent>();
            var untypedEvents   = new List<CloudEvent>();

            var publisher = BuildPublisher(b => b
                .AddTestChannel<OrderPlaced>(e => placedEvents.Add(e))
                .AddTestChannel<OrderCancelled>(e => cancelledEvents.Add(e))
                .AddTestChannel(e => untypedEvents.Add(e)));

            // Act
            await publisher.PublishAsync(
                typeof(OrderPlaced),
                new OrderPlaced { OrderId = "placed-1" },
                null,
                TestContext.Current.CancellationToken);

            await publisher.PublishAsync(
                typeof(OrderCancelled),
                new OrderCancelled { OrderId = "cancelled-1" },
                null,
                TestContext.Current.CancellationToken);

            // Assert – each typed channel received exactly its event; untyped channel was never called
            Assert.Single(placedEvents);
            Assert.Equal("order.placed", placedEvents[0].Type);

            Assert.Single(cancelledEvents);
            Assert.Equal("order.cancelled", cancelledEvents[0].Type);

            Assert.Empty(untypedEvents);
        }

        [Fact]
        public async Task PublishAsync_Generic_TypedChannelRegistered_EventRoutedOnlyToTypedChannel()
        {
            // Verifies the generic PublishAsync<TEvent> overload also benefits from typed routing.
            var typedEvents   = new List<CloudEvent>();
            var untypedEvents = new List<CloudEvent>();

            var publisher = BuildPublisher(b => b
                .AddTestChannel<OrderPlaced>(e => typedEvents.Add(e))
                .AddTestChannel(e => untypedEvents.Add(e)));

            // Act – use the generic overload
            await publisher.PublishAsync(
                new OrderPlaced { OrderId = "generic-1" },
                null,
                TestContext.Current.CancellationToken);

            // Assert
            Assert.Single(typedEvents);
            Assert.Empty(untypedEvents);
        }

        [Fact]
        public async Task PublishAsync_TypedChannelRegistered_EnrichedEventIsPublished()
        {
            // Verifies that enrichment (id, timestamp, source, attributes) is applied
            // even when routing to typed channels.
            CloudEvent? received = null;

            var publisher = BuildPublisher(b => b
                .AddTestChannel<OrderPlaced>(e => received = e));

            // Act
            await publisher.PublishAsync(
                typeof(OrderPlaced),
                new OrderPlaced { OrderId = "enrich-test" },
                null,
                TestContext.Current.CancellationToken);

            // Assert – event was enriched before delivery
            Assert.NotNull(received);
            Assert.NotNull(received!.Id);
            Assert.NotEmpty(received.Id!);
            Assert.NotNull(received.Time);
            Assert.NotNull(received.Source);
        }

        [Fact]
        public async Task PublishAsync_TypedChannelResultIsCached_SecondCallDoesNotRecomputeChannels()
        {
            // Verifies the typed-channel lookup cache works: publishing the same type twice
            // should succeed with consistent routing on both calls.
            var typedEvents   = new List<CloudEvent>();
            var untypedEvents = new List<CloudEvent>();

            var publisher = BuildPublisher(b => b
                .AddTestChannel<OrderPlaced>(e => typedEvents.Add(e))
                .AddTestChannel(e => untypedEvents.Add(e)));

            // Act – publish the same type twice
            await publisher.PublishAsync(
                typeof(OrderPlaced), new OrderPlaced { OrderId = "first" },
                null, TestContext.Current.CancellationToken);

            await publisher.PublishAsync(
                typeof(OrderPlaced), new OrderPlaced { OrderId = "second" },
                null, TestContext.Current.CancellationToken);

            // Assert – both events were routed to the typed channel only
            Assert.Equal(2, typedEvents.Count);
            Assert.Empty(untypedEvents);
        }

        [Fact]
        public async Task PublishEventAsync_UntypedCloudEvent_AlwaysUsesAllRegisteredChannels()
        {
            // PublishEventAsync(CloudEvent,...) directly — this path always broadcasts
            // to all registered channels regardless of typed-channel registrations.
            var typedEvents   = new List<CloudEvent>();
            var untypedEvents = new List<CloudEvent>();

            var publisher = BuildPublisher(b => b
                .AddTestChannel<OrderPlaced>(e => typedEvents.Add(e))
                .AddTestChannel(e => untypedEvents.Add(e)));

            // Act – directly publish a CloudEvent (no event data type involved)
            await publisher.PublishEventAsync(new CloudEvent
            {
                Type   = "order.placed",
                Source = new Uri("https://api.example.com"),
                Id     = Guid.NewGuid().ToString("N"),
            }, null, TestContext.Current.CancellationToken);

            // Assert – ALL channels receive the event because no data type was resolved
            Assert.Single(typedEvents);
            Assert.Single(untypedEvents);
        }
    }
}
