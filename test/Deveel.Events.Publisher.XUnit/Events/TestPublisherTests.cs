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

