// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using CloudNative.CloudEvents;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace Hermodr
{
    [Trait("Category", "Unit")]
    [Trait("Layer", "Application")]
    [Trait("Feature", "TestPublisher")]
    public class TestPublisherTests
    {
        // ── Helpers ───────────────────────────────────────────────────────────

        private static CloudEvent MakeEvent() => new()
        {
            Type            = "test.event",
            Source          = new Uri("https://api.example.com"),
            Id              = Guid.NewGuid().ToString("N"),
            DataContentType = "application/json",
            Data            = JsonSerializer.Serialize(new { name = "test" }),
        };

        // ── AddTestChannel (untyped) ──────────────────────────────────────────

        [Fact]
        public async Task Should_InvokeCallback_When_SyncActionCallbackIsRegistered()
        {
            // Arrange
            var receivedEvents = new List<CloudEvent>();
            var services = new ServiceCollection();
            services.AddEventPublisher()
                .AddTestChannel((CloudEvent e) => receivedEvents.Add(e));
            var publisher = services.BuildServiceProvider().GetRequiredService<EventPublisher>();

            // Act
            await publisher.PublishEventAsync(MakeEvent());

            // Assert
            Assert.Single(receivedEvents);
        }

        [Fact]
        public async Task Should_InvokeCallback_When_AsyncFuncCallbackIsRegistered()
        {
            // Arrange
            var receivedEvents = new List<CloudEvent>();
            var services = new ServiceCollection();
            services.AddEventPublisher()
                .AddTestChannel((CloudEvent e) =>
                {
                    receivedEvents.Add(e);
                    return Task.CompletedTask;
                });
            var publisher = services.BuildServiceProvider().GetRequiredService<EventPublisher>();

            // Act
            await publisher.PublishEventAsync(MakeEvent());

            // Assert
            Assert.Single(receivedEvents);
        }

        [Fact]
        public async Task Should_InvokeCallback_When_IEventPublishCallbackIsRegistered()
        {
            // Arrange
            var callback = new CountingCallback();
            var services = new ServiceCollection();
            services.AddEventPublisher()
                .AddTestChannel(callback);
            var publisher = services.BuildServiceProvider().GetRequiredService<EventPublisher>();

            // Act
            await publisher.PublishEventAsync(MakeEvent());

            // Assert
            Assert.Equal(1, callback.Count);
        }

        [Fact]
        public async Task Should_InvokeAsyncCallback_When_AsyncIEventPublishCallbackIsRegistered()
        {
            // Arrange
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

            // Act
            await publisher.PublishEventAsync(MakeEvent());

            // Assert
            Assert.Single(receivedEvents);
        }

        // ── AddTestChannel (typed) ────────────────────────────────────────────

        [Fact]
        public async Task Should_InvokeSyncCallback_When_TypedSyncCallbackIsRegistered()
        {
            // Arrange
            var received = new List<CloudEvent>();
            var services = new ServiceCollection();
            services.AddEventPublisher(o => o.Source = new Uri("https://api.example.com"))
                .AddTestChannel<OrderPlaced>(e => received.Add(e));
            var publisher = services.BuildServiceProvider().GetRequiredService<EventPublisher>();

            // Act
            await publisher.PublishAsync(new OrderPlaced { OrderId = "T-001" });

            // Assert
            Assert.Single(received);
        }

        [Fact]
        public async Task Should_InvokeAsyncCallback_When_TypedAsyncCallbackIsRegistered()
        {
            // Arrange
            var received = new List<CloudEvent>();
            var services = new ServiceCollection();
            services.AddEventPublisher(o => o.Source = new Uri("https://api.example.com"))
                .AddTestChannel<OrderPlaced>((CloudEvent e) =>
                {
                    received.Add(e);
                    return Task.CompletedTask;
                });
            var publisher = services.BuildServiceProvider().GetRequiredService<EventPublisher>();

            // Act
            await publisher.PublishAsync(new OrderPlaced { OrderId = "T-002" });

            // Assert
            Assert.Single(received);
        }

        [Fact]
        public async Task Should_OnlyReceiveMatchingEventType_When_MultipleTypedChannelsAreRegistered()
        {
            // Arrange
            var orderReceived = new List<CloudEvent>();
            var userReceived  = new List<CloudEvent>();
            var services = new ServiceCollection();
            services.AddEventPublisher(o => o.Source = new Uri("https://api.example.com"))
                .AddTestChannel<OrderPlaced>(e => orderReceived.Add(e))
                .AddTestChannel<UserCreated>(e => userReceived.Add(e));
            var publisher = services.BuildServiceProvider().GetRequiredService<EventPublisher>();

            // Act
            await publisher.PublishAsync(new OrderPlaced { OrderId = "X-1" });

            // Assert
            Assert.Single(orderReceived);
            Assert.Empty(userReceived);
        }

        [Fact]
        public async Task Should_EnrichEvent_When_TypedEventIsPublished()
        {
            // Arrange
            CloudEvent? received = null;
            var services = new ServiceCollection();
            services.AddEventPublisher(o => o.Source = new Uri("https://api.example.com"))
                .AddTestChannel<OrderPlaced>(e => received = e);
            var publisher = services.BuildServiceProvider().GetRequiredService<EventPublisher>();

            // Act
            await publisher.PublishAsync(new OrderPlaced { OrderId = "enrich-1" });

            // Assert
            Assert.NotNull(received);
            Assert.Equal("order.placed", received!.Type);
            Assert.NotNull(received.Id);
            Assert.NotEmpty(received.Id!);
            Assert.NotNull(received.Source);
        }

        // ── Domain types ──────────────────────────────────────────────────────

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
