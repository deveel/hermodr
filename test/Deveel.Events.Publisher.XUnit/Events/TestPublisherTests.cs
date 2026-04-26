// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using CloudNative.CloudEvents;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace Deveel.Events
{
    public class TestPublisherTests
    {
        private static CloudEvent MakeEvent() => new()
        {
            Type = "test.event",
            Source = new Uri("https://api.example.com"),
            Id = Guid.NewGuid().ToString("N"),
            DataContentType = "application/json",
            Data = JsonSerializer.Serialize(new { name = "test" }),
        };

        [Fact]
        public async Task AddTestChannel_WithActionCallback_IsInvoked()
        {
            var receivedEvents = new List<CloudEvent>();
            var services = new ServiceCollection();
            services.AddEventPublisher()
                .AddTestChannel((CloudEvent e) => receivedEvents.Add(e));

            var publisher = services.BuildServiceProvider().GetRequiredService<EventPublisher>();
            await publisher.PublishEventAsync(MakeEvent());

            Assert.Single(receivedEvents);
        }

        [Fact]
        public async Task AddTestChannel_WithAsyncFuncCallback_IsInvoked()
        {
            var receivedEvents = new List<CloudEvent>();
            var services = new ServiceCollection();
            services.AddEventPublisher()
                .AddTestChannel((CloudEvent e) =>
                {
                    receivedEvents.Add(e);
                    return Task.CompletedTask;
                });

            var publisher = services.BuildServiceProvider().GetRequiredService<EventPublisher>();
            await publisher.PublishEventAsync(MakeEvent());

            Assert.Single(receivedEvents);
        }

        [Fact]
        public async Task AddTestChannel_WithIEventPublishCallback_IsInvoked()
        {
            var callback = new CountingCallback();
            var services = new ServiceCollection();
            services.AddEventPublisher()
                .AddTestChannel(callback);

            var publisher = services.BuildServiceProvider().GetRequiredService<EventPublisher>();
            await publisher.PublishEventAsync(MakeEvent());

            Assert.Equal(1, callback.Count);
        }

        [Fact]
        public async Task AddTestChannel_WithIEventPublishCallback_AsyncCallback()
        {
            var receivedEvents = new List<CloudEvent>();
            var callback = new AsyncCallback(e =>
            {
                receivedEvents.Add(e);
                return Task.CompletedTask;
            });
            var services = new ServiceCollection();
            services.AddEventPublisher()
                .AddTestChannel(callback);

            var publisher = services.BuildServiceProvider().GetRequiredService<EventPublisher>();
            await publisher.PublishEventAsync(MakeEvent());

            Assert.Single(receivedEvents);
        }

        // ── TypedTestEventPublishChannel ──────────────────────────────────────

        [Fact]
        public async Task AddTestChannel_Typed_SyncCallback_IsInvoked()
        {
            var received = new List<CloudEvent>();
            var services = new ServiceCollection();
            services.AddEventPublisher(o => o.Source = new Uri("https://api.example.com"))
                .AddTestChannel<OrderPlaced>(e => received.Add(e));

            var publisher = services.BuildServiceProvider().GetRequiredService<EventPublisher>();
            await publisher.PublishAsync(new OrderPlaced { OrderId = "T-001" });

            Assert.Single(received);
        }

        [Fact]
        public async Task AddTestChannel_Typed_AsyncCallback_IsInvoked()
        {
            var received = new List<CloudEvent>();
            var services = new ServiceCollection();
            services.AddEventPublisher(o => o.Source = new Uri("https://api.example.com"))
                .AddTestChannel<OrderPlaced>((CloudEvent e) =>
                {
                    received.Add(e);
                    return Task.CompletedTask;
                });

            var publisher = services.BuildServiceProvider().GetRequiredService<EventPublisher>();
            await publisher.PublishAsync(new OrderPlaced { OrderId = "T-002" });

            Assert.Single(received);
        }

        [Fact]
        public async Task AddTestChannel_Typed_OnlyReceivesMatchingEventType()
        {
            var orderReceived = new List<CloudEvent>();
            var userReceived  = new List<CloudEvent>();

            var services = new ServiceCollection();
            services.AddEventPublisher(o => o.Source = new Uri("https://api.example.com"))
                .AddTestChannel<OrderPlaced>(e => orderReceived.Add(e))
                .AddTestChannel<UserCreated>(e => userReceived.Add(e));

            var publisher = services.BuildServiceProvider().GetRequiredService<EventPublisher>();

            await publisher.PublishAsync(new OrderPlaced { OrderId = "X-1" });

            // Only the typed OrderPlaced channel should receive the event
            Assert.Single(orderReceived);
            Assert.Empty(userReceived);
        }

        [Fact]
        public async Task AddTestChannel_Typed_ReceivedEventIsEnriched()
        {
            CloudEvent? received = null;

            var services = new ServiceCollection();
            services.AddEventPublisher(o => o.Source = new Uri("https://api.example.com"))
                .AddTestChannel<OrderPlaced>(e => received = e);

            var publisher = services.BuildServiceProvider().GetRequiredService<EventPublisher>();
            await publisher.PublishAsync(new OrderPlaced { OrderId = "enrich-1" });

            Assert.NotNull(received);
            Assert.Equal("order.placed", received!.Type);
            Assert.NotNull(received.Id);
            Assert.NotEmpty(received.Id!);
            Assert.NotNull(received.Source);
        }

        [Event("order.placed", "https://example.com/events/order.placed/1.0")]
        private class OrderPlaced
        {
            public string OrderId { get; set; } = string.Empty;
        }

        [Event("user.created", "https://example.com/events/user.created/1.0")]
        private class UserCreated
        {
            public string UserId { get; set; } = string.Empty;
        }

        private sealed class CountingCallback : IEventPublishCallback
        {
            public int Count { get; private set; }
            public Task OnEventPublishedAsync(CloudEvent @event)
            {
                Count++;
                return Task.CompletedTask;
            }
        }

        private sealed class AsyncCallback : IEventPublishCallback
        {
            private readonly Func<CloudEvent, Task> _fn;
            public AsyncCallback(Func<CloudEvent, Task> fn) => _fn = fn;
            public Task OnEventPublishedAsync(CloudEvent @event) => _fn(@event);
        }
    }
}

