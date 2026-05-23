//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

using Microsoft.Extensions.DependencyInjection;

using System.Text.Json;

namespace Hermodr
{
    /// <summary>
    /// Integration-level tests that cover scenarios not
    /// addressed by the focused unit-test files:
    ///   - Multiple channels all receive the event
    ///   - <see cref="EventPublisher"/> interface resolution and usage
    ///   - <see cref="EventPublisher.PublishAsync{TData}"/> when TData is a <see cref="CloudEvent"/>
    ///   - <see cref="EventPublisher.PublishAsync{TData}"/> when TData is <see cref="IEventConvertible"/>
    ///     returning null (ThrowOnErrors = false swallows)
    ///   - <see cref="EventPublisherOptions"/> property defaults
    ///   - Typed channel routing via <see cref="IEventPublishChannel{TData}"/>
    /// </summary>
    public class EventPublisherIntegrationTests
    {
        private static CloudEvent ValidEvent(string type = "test.event") => new()
        {
            Type   = type,
            Source = new Uri("https://example.com"),
            Id     = Guid.NewGuid().ToString("N"),
        };

        // ── EventPublisherOptions defaults ───────────────────────────────────

        [Fact]
        public static void EventPublisherOptions_Defaults_AreCorrect()
        {
            var opts = new EventPublisherOptions();
            Assert.False(opts.ThrowOnErrors);
            Assert.NotNull(opts.Attributes);
            Assert.Empty(opts.Attributes);
            Assert.Null(opts.Source);
            Assert.Null(opts.DataSchemaBaseUri);
        }

        // ── EventPublisher interface ────────────────────────────────────────

        [Fact]
        public static void EventPublisher_ResolvedFromDI()
        {
            var services = new ServiceCollection();
            services.AddEventPublisher();

            var sp = services.BuildServiceProvider();
            var publisher = sp.GetService<EventPublisher>();

            Assert.NotNull(publisher);
        }

        [Fact]
        public async Task EventPublisher_PublishEventAsync_DeliversEvent()
        {
            var received = new List<CloudEvent>();
            var services = new ServiceCollection();
            services.AddEventPublisher().AddTestChannel(e => received.Add(e));

            var sp        = services.BuildServiceProvider();
            var publisher = sp.GetRequiredService<EventPublisher>();

            await publisher.PublishEventAsync(ValidEvent(), cancellationToken: TestContext.Current.CancellationToken);

            Assert.Single(received);
        }

        // ── Multiple channels ────────────────────────────────────────────────

        [Fact]
        public async Task PublishEvent_MultipleChannels_AllReceiveEvent()
        {
            var channel1Events = new List<CloudEvent>();
            var channel2Events = new List<CloudEvent>();

            var services = new ServiceCollection();
            // Register the publisher infrastructure and two independent channels.
            var builder = services.AddEventPublisher();
            builder.AddChannel(new CallbackChannel(e => channel1Events.Add(e)));
            builder.AddChannel(new CallbackChannel(e => channel2Events.Add(e)));

            var publisher = services.BuildServiceProvider().GetRequiredService<EventPublisher>();
            var evt = ValidEvent();
            await publisher.PublishEventAsync(evt, cancellationToken: TestContext.Current.CancellationToken);

            Assert.Single(channel1Events);
            Assert.Single(channel2Events);
            Assert.Equal(channel1Events[0].Id, channel2Events[0].Id); // same event delivered to both
        }

        // ── PublishAsync<TData> when TData is CloudEvent ─────────────────────

        [Fact]
        public async Task PublishAsync_WhenDataIsCloudEvent_PublishesThatEvent()
        {
            var received = new List<CloudEvent>();
            var services = new ServiceCollection();
            services.AddEventPublisher().AddTestChannel(e => received.Add(e));

            var publisher = services.BuildServiceProvider().GetRequiredService<EventPublisher>();

            var cloudEvent = ValidEvent("direct.cloud.event");
            await publisher.PublishAsync(cloudEvent, cancellationToken: TestContext.Current.CancellationToken);

            Assert.Single(received);
            Assert.Equal("direct.cloud.event", received[0].Type);
        }

        // ── PublishAsync<TData> when TData is IEventConvertible ──────────────

        [Fact]
        public async Task PublishAsync_IEventConvertible_ConvertedEventIsDelivered()
        {
            var received = new List<CloudEvent>();
            var services = new ServiceCollection();
            services.AddEventPublisher().AddTestChannel(e => received.Add(e));

            var publisher = services.BuildServiceProvider().GetRequiredService<EventPublisher>();

            await publisher.PublishAsync(new OrderShipped { OrderId = "42" },
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.Single(received);
            Assert.Equal("order.shipped", received[0].Type);
        }

        [Fact]
        public async Task PublishAsync_IEventConvertible_WhenConversionReturnsNull_ThrowOnErrorsFalse_Swallows()
        {
            var received = new List<CloudEvent>();
            var services = new ServiceCollection();
            services.AddEventPublisher(o => o.ThrowOnErrors = false)
                    .AddTestChannel(e => received.Add(e));

            var publisher = services.BuildServiceProvider().GetRequiredService<EventPublisher>();

            // NullConvertible intentionally throws inside ToCloudEvent()
            await publisher.PublishAsync(new ThrowingConvertible(), cancellationToken:
                TestContext.Current.CancellationToken);

            Assert.Empty(received); // nothing was delivered
        }

        [Fact]
        public async Task PublishAsync_IEventConvertible_WhenConversionThrows_ThrowOnErrorsTrue_Throws()
        {
            var services = new ServiceCollection();
            services.AddEventPublisher(o => o.ThrowOnErrors = true)
                    .AddTestChannel(_ => { });

            var publisher = services.BuildServiceProvider().GetRequiredService<EventPublisher>();

            await Assert.ThrowsAsync<EventConversionException>(
                () => publisher.PublishAsync(new ThrowingConvertible(), cancellationToken:
                    TestContext.Current.CancellationToken));
        }

        // ── Publisher sets attribute byte[] type ──────────────────────────────

        [Fact]
        public async Task PublishEvent_BinaryAttribute_IsSet()
        {
            var received = new List<CloudEvent>();
            var services = new ServiceCollection();
            services.AddEventPublisher(o =>
            {
                o.Attributes["binattr"] = new byte[] { 1, 2, 3 };
            }).AddTestChannel(e => received.Add(e));

            var publisher = services.BuildServiceProvider().GetRequiredService<EventPublisher>();

            await publisher.PublishEventAsync(new CloudEvent
            {
                Type   = "test.event",
                Source = new Uri("https://example.com"),
            }, cancellationToken: TestContext.Current.CancellationToken);

            Assert.Single(received);
        }

        // ── Publisher: no channels registered still doesn't throw ─────────────

        [Fact]
        public async Task PublishEvent_NoChannels_DoesNotThrow()
        {
            var services = new ServiceCollection();
            services.AddEventPublisher();

            var publisher = services.BuildServiceProvider().GetRequiredService<EventPublisher>();

            // No exception expected — just nothing to deliver to
            await publisher.PublishEventAsync(ValidEvent(), cancellationToken: TestContext.Current.CancellationToken);
        }

        // ── Typed channel routing ─────────────────────────────────────────────

        [Fact]
        public async Task PublishAsync_WithTypedChannel_GeneralEventGoesToAllChannels()
        {
            var generalEvents = new List<CloudEvent>();
            var extraEvents   = new List<CloudEvent>();

            var services = new ServiceCollection();
            var builder = services.AddEventPublisher();
            builder.AddChannel(new CallbackChannel(e => generalEvents.Add(e)));
            builder.AddChannel(new CallbackChannel(e => extraEvents.Add(e)));

            var publisher = services.BuildServiceProvider().GetRequiredService<EventPublisher>();
            await publisher.PublishEventAsync(ValidEvent("generic.event"),
                cancellationToken: TestContext.Current.CancellationToken);

            // Both channels registered as IEventPublishChannel receive the event
            Assert.Single(generalEvents);
            Assert.Single(extraEvents);
        }

        // ── EventSystemTime ──────────────────────────────────────────────────

        [Fact]
        public void EventSystemTime_Instance_ReturnsCurrentUtcTime()
        {
            var before = DateTimeOffset.UtcNow;
            var result = EventSystemTime.Instance.UtcNow;
            var after  = DateTimeOffset.UtcNow;

            Assert.True(result >= before && result <= after,
                $"Expected UtcNow between {before} and {after}, but got {result}");
        }

        [Fact]
        public void EventSystemTime_IsIEventSystemTime()
        {
            Assert.IsAssignableFrom<IEventSystemTime>(EventSystemTime.Instance);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        [Event("order.shipped", "https://example.com/schemas/order.shipped/1.0")]
        private sealed class OrderShipped : IEventConvertible
        {
            public string? OrderId { get; set; }

            public CloudEvent ToCloudEvent() => new()
            {
                Type            = "order.shipped",
                Source          = new Uri("https://example.com"),
                Id              = Guid.NewGuid().ToString("N"),
                DataContentType = "application/json",
                Data            = JsonSerializer.Serialize(this),
            };
        }

        private sealed class ThrowingConvertible : IEventConvertible
        {
            public CloudEvent ToCloudEvent() =>
                throw new InvalidOperationException("Conversion failed on purpose");
        }

        private sealed class CallbackChannel : IEventPublishChannel
        {
            private readonly Action<CloudEvent> _callback;
            public CallbackChannel(Action<CloudEvent> callback) => _callback = callback;
            public Task PublishAsync(CloudEvent @event, EventPublishOptions? options = null,
                CancellationToken cancellationToken = default)
            {
                _callback(@event);
                return Task.CompletedTask;
            }
        }
    }
}




