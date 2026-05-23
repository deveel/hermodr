//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Text.Json;

using CloudNative.CloudEvents;

using Deveel.Filters;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Hermodr
{
    [Trait("Feature", "Subscriptions")]
    public static class EventFilterTests
    {
        private static CloudEvent MakeEvent(
            string type = "com.example.order.placed",
            string source = "https://example.com/api",
            string? subject = null)
        {
            var e = new CloudEvent
            {
                Type = type,
                Source = new Uri(source),
                Id = Guid.NewGuid().ToString("N"),
                Subject = subject
            };
            e.DataContentType = "application/json";
            return e;
        }

        // ── FilterExpression type filters ───────────────────────────────────────────

        [Fact]
        public static void ExactTypeFilter_MatchesExactValue()
        {
            var filter = EventFilter.ByType("com.example.order.placed");
            Assert.True(filter.Matches(MakeEvent("com.example.order.placed"), EventSubscriptionContext.Empty));
        }

        [Fact]
        public static void ExactTypeFilter_DoesNotMatchDifferentValue()
        {
            var filter = EventFilter.ByType("com.example.order.placed");
            Assert.False(filter.Matches(MakeEvent("com.example.order.updated"), EventSubscriptionContext.Empty));
        }

        [Fact]
        public static void PrefixTypeFilter_MatchesStartingValue()
        {
            var filter = EventFilter.ByTypePattern("com.example.*");
            Assert.True(filter.Matches(MakeEvent("com.example.order.placed"), EventSubscriptionContext.Empty));
        }

        [Fact]
        public static void PrefixTypeFilter_DoesNotMatchNonStartingValue()
        {
            var filter = EventFilter.ByTypePattern("com.example.*");
            Assert.False(filter.Matches(MakeEvent("org.other.something"), EventSubscriptionContext.Empty));
        }

        [Fact]
        public static void SuffixTypeFilter_MatchesEndingValue()
        {
            var filter = EventFilter.ByTypePattern("*.placed");
            Assert.True(filter.Matches(MakeEvent("com.example.order.placed"), EventSubscriptionContext.Empty));
        }

        [Fact]
        public static void SuffixTypeFilter_DoesNotMatchNonEndingValue()
        {
            var filter = EventFilter.ByTypePattern("*.placed");
            Assert.False(filter.Matches(MakeEvent("com.example.order.updated"), EventSubscriptionContext.Empty));
        }

        // ── EventFilter / FilterExpression.Matches ────────────────────────────────────────────────────────

        [Fact]
        public static void EmptyFilter_MatchesAnyEvent()
        {
            var filter = FilterExpression.Constant(true);
            Assert.True(filter.Matches(MakeEvent(), EventSubscriptionContext.Empty));
        }

        [Fact]
        public static void TypeFilter_Exact_Matches()
        {
            var filter = EventFilter.ByType("com.example.order.placed");
            Assert.True(filter.Matches(MakeEvent(type: "com.example.order.placed"), EventSubscriptionContext.Empty));
        }

        [Fact]
        public static void TypeFilter_Exact_DoesNotMatchOtherType()
        {
            var filter = EventFilter.ByType("com.example.order.placed");
            Assert.False(filter.Matches(MakeEvent(type: "com.example.order.updated"), EventSubscriptionContext.Empty));
        }

        [Fact]
        public static void TypeFilter_Prefix_Wildcard_Matches()
        {
            var filter = EventFilter.ByTypePattern("com.example.*");
            Assert.True(filter.Matches(MakeEvent(type: "com.example.order.placed"), EventSubscriptionContext.Empty));
            Assert.True(filter.Matches(MakeEvent(type: "com.example.user.created"), EventSubscriptionContext.Empty));
        }

        [Fact]
        public static void TypeFilter_Prefix_Wildcard_DoesNotMatchOtherNamespace()
        {
            var filter = EventFilter.ByTypePattern("com.example.*");
            Assert.False(filter.Matches(MakeEvent(type: "org.other.event"), EventSubscriptionContext.Empty));
        }

        [Fact]
        public static void SourceFilter_Matches()
        {
            var filter = EventFilter.BySource("https://example.com/api");
            Assert.True(filter.Matches(MakeEvent(source: "https://example.com/api"), EventSubscriptionContext.Empty));
        }

        [Fact]
        public static void SourceFilter_DoesNotMatchOtherSource()
        {
            var filter = EventFilter.BySource("https://example.com/api");
            Assert.False(filter.Matches(MakeEvent(source: "https://other.com/api"), EventSubscriptionContext.Empty));
        }

        [Fact]
        public static void SubjectFilter_Matches()
        {
            var filter = EventFilter.BySubject("orders/42");
            Assert.True(filter.Matches(MakeEvent(subject: "orders/42"), EventSubscriptionContext.Empty));
        }

        [Fact]
        public static void SubjectFilter_DoesNotMatchMissingSubject()
        {
            var filter = EventFilter.BySubject("orders/42");
            Assert.False(filter.Matches(MakeEvent(), EventSubscriptionContext.Empty));
        }

        [Fact]
        public static void CombinedFilter_AllCriteriaMustPass()
        {
            var filter = EventFilter.All(
                EventFilter.ByTypePattern("com.example.*"),
                EventFilter.BySource("https://example.com/api"),
                EventFilter.BySubjectPattern("orders/*"));

            var matching = MakeEvent(
                type: "com.example.order.placed",
                source: "https://example.com/api",
                subject: "orders/99");

            var wrongSource = MakeEvent(
                type: "com.example.order.placed",
                source: "https://other.com",
                subject: "orders/99");

            Assert.True(filter.Matches(matching, EventSubscriptionContext.Empty));
            Assert.False(filter.Matches(wrongSource, EventSubscriptionContext.Empty));
        }

        [Fact]
        public static void Filter_ReturnsFalseForNullEvent()
        {
            var filter = FilterExpression.Constant(true);
            Assert.False(filter.Matches(null!, EventSubscriptionContext.Empty));
        }
    }

    // ────────────────────────────────────────────────────────────────────────────────

    [Trait("Feature", "Subscriptions")]
    public static class EventSubscriptionRegistryTests
    {
        private static CloudEvent MakeEvent(string type = "com.example.test")
            => new()
            {
                Type = type,
                Source = new Uri("https://example.com"),
                Id = Guid.NewGuid().ToString("N")
            };

        [Fact]
        public static void Register_AndRetrieve_MatchingSubscription()
        {
            var registry = new EventSubscriptionRegistry();
            var sub = new EventSubscription(
                EventFilter.ByType("com.example.test"),
                (_, _) => Task.CompletedTask,
                "test-sub");

            registry.Register(sub);

            var matches = registry.GetMatchingSubscriptions(MakeEvent("com.example.test"));
            Assert.Single(matches);
            Assert.Same(sub, matches[0]);
        }

        [Fact]
        public static void Register_DoesNotReturnNonMatchingSubscription()
        {
            var registry = new EventSubscriptionRegistry();
            var sub = new EventSubscription(
                EventFilter.ByType("com.example.other"),
                (_, _) => Task.CompletedTask);

            registry.Register(sub);

            var matches = registry.GetMatchingSubscriptions(MakeEvent("com.example.test"));
            Assert.Empty(matches);
        }

        [Fact]
        public static void Constructor_AcceptsPreSeededSubscriptions()
        {
            var sub = new EventSubscription(
                FilterExpression.Constant(true),
                (_, _) => Task.CompletedTask);

            var registry = new EventSubscriptionRegistry([sub]);
            var matches = registry.GetMatchingSubscriptions(MakeEvent());
            Assert.Single(matches);
        }

        [Fact]
        public static void MultipleSubscriptions_AllMatchingAreReturned()
        {
            var registry = new EventSubscriptionRegistry();

            var sub1 = new EventSubscription(EventFilter.ByTypePattern("com.example.*"),
                (_, _) => Task.CompletedTask, "sub1");
            var sub2 = new EventSubscription(EventFilter.ByType("com.example.test"),
                (_, _) => Task.CompletedTask, "sub2");
            var sub3 = new EventSubscription(EventFilter.ByType("com.example.other"),
                (_, _) => Task.CompletedTask, "sub3");

            registry.Register(sub1);
            registry.Register(sub2);
            registry.Register(sub3);

            var matches = registry.GetMatchingSubscriptions(MakeEvent("com.example.test"));
            Assert.Equal(2, matches.Count);
            Assert.Contains(sub1, matches);
            Assert.Contains(sub2, matches);
            Assert.DoesNotContain(sub3, matches);
        }
    }

    // ────────────────────────────────────────────────────────────────────────────────

    [Trait("Feature", "Subscriptions")]
    public class EventDispatcherTests
    {
        private static CloudEvent MakeEvent(string type = "com.example.test")
            => new()
            {
                Type = type,
                Source = new Uri("https://example.com"),
                Id = Guid.NewGuid().ToString("N")
            };

        [Fact]
        public async Task Publish_WithDispatcher_InvokesMatchingHandler()
        {
            var handled = new List<CloudEvent>();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventPublisher(opt => opt.Source = new Uri("https://example.com"))
                .AddSubscriptions()
                .Subscribe(
                    EventFilter.ByType("com.example.test"),
                    (e, _) => { handled.Add(e); return Task.CompletedTask; },
                    "sub");

            var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<EventPublisher>().UseDispatcher();
            var @event = MakeEvent();
            await publisher.PublishEventAsync(@event);

            Assert.Single(handled);
            Assert.Same(@event, handled[0]);
        }

        [Fact]
        public async Task Publish_WithDispatcher_DoesNotInvokeNonMatchingHandler()
        {
            var handled = false;

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventPublisher(opt => opt.Source = new Uri("https://example.com"))
                .AddSubscriptions()
                .Subscribe(
                    EventFilter.ByType("com.example.other"),
                    (_, _) => { handled = true; return Task.CompletedTask; });

            var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<EventPublisher>().UseDispatcher();

            await publisher.PublishEventAsync(MakeEvent("com.example.test"));

            Assert.False(handled);
        }

        [Fact]
        public async Task Publish_WithDispatcher_DefaultOptions_DoesNotThrowOnHandlerError()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventPublisher(opt => opt.Source = new Uri("https://example.com"))
                .AddSubscriptions()
                .Subscribe(
                    FilterExpression.Constant(true),
                    (_, _) => throw new InvalidOperationException("boom"));

            var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<EventPublisher>().UseDispatcher();

            await publisher.PublishEventAsync(MakeEvent());
        }

        [Fact]
        public async Task Publish_WithDispatcher_ThrowOnHandlerError_PropagatesException()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventPublisher(opt => opt.Source = new Uri("https://example.com"))

                .AddSubscriptions(o => o.ThrowOnHandlerError = true)
                .Subscribe(
                    FilterExpression.Constant(true),
                    (_, _) => throw new InvalidOperationException("boom"));

            var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<EventPublisher>().UseDispatcher();

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                publisher.PublishEventAsync(MakeEvent()));
        }

        [Fact]
        public async Task Publish_WithDispatcher_MultipleSubscribers_AllInvoked()
        {
            var invoked = new List<string>();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventPublisher(opt => opt.Source = new Uri("https://example.com"))
                .AddSubscriptions()
                .Subscribe(
                    FilterExpression.Constant(true),
                    (_, _) => { invoked.Add("sub1"); return Task.CompletedTask; },
                    "sub1")
                .Subscribe(
                    FilterExpression.Constant(true),
                    (_, _) => { invoked.Add("sub2"); return Task.CompletedTask; },
                    "sub2");

            var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<EventPublisher>().UseDispatcher();
            await publisher.PublishEventAsync(MakeEvent());

            Assert.Equal(["sub1", "sub2"], invoked);
        }

        [Fact]
        public async Task AsMiddleware_DispatchesEvent()
        {
            var handled = false;

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventPublisher(opt => opt.Source = new Uri("https://example.com"))
                .AddSubscriptions()
                .Subscribe(FilterExpression.Constant(true),
                    (_, _) => { handled = true; return Task.CompletedTask; });

            var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<EventPublisher>()
                .UseDispatcher();

            await publisher.PublishEventAsync(MakeEvent());

            Assert.True(handled);
        }

        [Fact]
        public async Task Publish_WithDispatcher_RespectsCancellation()
        {
            using var cts = new CancellationTokenSource();
            var handlerStarted = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventPublisher(opt => opt.Source = new Uri("https://example.com"))
                .AddSubscriptions()
                .Subscribe(
                    FilterExpression.Constant(true),
                    async (_, ct) =>
                    {
                        handlerStarted.TrySetResult(null);
                        await Task.Delay(Timeout.Infinite, ct);
                    });

            var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<EventPublisher>().UseDispatcher();

            var task = publisher.PublishEventAsync(MakeEvent(), cancellationToken: cts.Token);

            // Wait for the handler to start before cancelling
            await handlerStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        }
    }

    // ────────────────────────────────────────────────────────────────────────────────

    [Trait("Feature", "Subscriptions")]
    public static class EventDispatcherDiTests
    {
        private static CloudEvent MakeEvent(string type = "com.example.test")
            => new()
            {
                Type = type,
                Source = new Uri("https://example.com"),
                Id = Guid.NewGuid().ToString("N")
            };

        [Fact]
        public static async Task AddSubscriptions_WithDispatcherOptions_ThrowOnHandlerError_Propagates()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventPublisher(opt => opt.Source = new Uri("https://example.com"))
                    .AddSubscriptions(o => o.ThrowOnHandlerError = true)
                    .Subscribe(FilterExpression.Constant(true),
                        (_, _) => throw new InvalidOperationException("intentional"));

            var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<EventPublisher>().UseDispatcher();

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                publisher.PublishEventAsync(new CloudEvent
                {
                    Type   = "com.example.test",
                    Source = new Uri("https://example.com"),
                    Id     = Guid.NewGuid().ToString("N"),
                }));
        }

        [Fact]
        public static async Task PublishEventAsync_NullEvent_Throws()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventPublisher().AddSubscriptions();

            var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<EventPublisher>().UseDispatcher();

            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                publisher.PublishEventAsync(null!));
        }

        [Fact]
        public static void AddSubscriptions_RegistersRequiredServices()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventPublisher()
                    .AddSubscriptions();

            var provider = services.BuildServiceProvider();

            Assert.NotNull(provider.GetService<IEventSubscriptionRegistry>());
            Assert.NotNull(provider.GetService<IEventSubscriptionResolver>());
        }

        [Fact]
        public static async Task AddSubscriptions_WithInlineSubscription_RoutesEvent()
        {
            var handled = new List<string>();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventPublisher(opt =>
                {
                    opt.Source = new Uri("https://example.com");
                    opt.ThrowOnErrors = true;
                })
                .AddSubscriptions()
                .Subscribe("com.example.test",
                    (e, _) => { handled.Add(e.Type!); return Task.CompletedTask; },
                    name: "test-handler");

            var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<EventPublisher>()
                .UseDispatcher();

            var @event = MakeEvent();
            @event.Time = DateTimeOffset.UtcNow;

            await publisher.PublishEventAsync(@event);

            Assert.Single(handled);
            Assert.Equal("com.example.test", handled[0]);
        }

        [Fact]
        public static async Task AddSubscriptions_WithPatternSubscription_RoutesMatchingEvents()
        {
            var handled = new List<string>();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventPublisher(opt => opt.Source = new Uri("https://example.com"))
                .AddSubscriptions()
                .Subscribe("com.example.*",
                    (e, _) => { handled.Add(e.Type!); return Task.CompletedTask; });

            var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<EventPublisher>()
                .UseDispatcher();

            CloudEvent Evt(string type) => new()
            {
                Type = type,
                Source = new Uri("https://example.com"),
                Id = Guid.NewGuid().ToString("N"),
                Time = DateTimeOffset.UtcNow
            };

            await publisher.PublishEventAsync(Evt("com.example.order.placed"));
            await publisher.PublishEventAsync(Evt("com.example.user.created"));
            await publisher.PublishEventAsync(Evt("org.other.irrelevant"));

            Assert.Equal(2, handled.Count);
            Assert.Contains("com.example.order.placed", handled);
            Assert.Contains("com.example.user.created", handled);
        }

        [Fact]
        public static async Task AddSubscriptions_WithFilterBuilder_RoutesMatchingEvents()
        {
            var handled = false;

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventPublisher(opt => opt.Source = new Uri("https://example.com"))
                .AddSubscriptions()
                .Subscribe(
                    EventFilter.All(
                        EventFilter.ByTypePattern("com.example.*"),
                        // Uri.ToString() normalises "https://example.com" → "https://example.com/"
                        EventFilter.BySource("https://example.com/")),
                    (_, _) => { handled = true; return Task.CompletedTask; });

            var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<EventPublisher>()
                .UseDispatcher();

            await publisher.PublishEventAsync(new CloudEvent
            {
                Type = "com.example.test",
                Source = new Uri("https://example.com"),
                Id = Guid.NewGuid().ToString("N"),
                Time = DateTimeOffset.UtcNow
            });

            Assert.True(handled);
        }

        [Fact]
        public static async Task AddSubscriptions_WithHandlerClass_RoutesEvents()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventPublisher(opt => opt.Source = new Uri("https://example.com"))
                .AddSubscriptions()
                .Subscribe<OrderPlacedSubscription>();

            var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<EventPublisher>()
                .UseDispatcher();
            var subscription = provider.GetRequiredService<OrderPlacedSubscription>();

            await publisher.PublishEventAsync(new CloudEvent
            {
                Type = "com.example.order.placed",
                Source = new Uri("https://example.com"),
                Id = Guid.NewGuid().ToString("N"),
                Time = DateTimeOffset.UtcNow
            });

            Assert.True(subscription.WasInvoked);
        }

        // ── helpers ────────────────────────────────────────────────────────────────

        private sealed class OrderPlacedSubscription : IEventSubscription
        {
            public bool WasInvoked { get; private set; }

            public string? Name => nameof(OrderPlacedSubscription);

            public FilterExpression Filter =>
                EventFilter.ByType("com.example.order.placed");

            public Task HandleAsync(CloudEvent @event, CancellationToken cancellationToken = default)
            {
                WasInvoked = true;
                return Task.CompletedTask;
            }
        }
    }

    // ────────────────────────────────────────────────────────────────────────────────

    [Trait("Feature", "Subscriptions")]
    public static class EventDataFilterTests
    {
        private static CloudEvent JsonEvent(object? data, string contentType = "application/json")
        {
            var e = new CloudEvent
            {
                Type = "com.example.test",
                Source = new Uri("https://example.com"),
                Id = Guid.NewGuid().ToString("N"),
                DataContentType = contentType,
                Data = data
            };
            return e;
        }

        private static CloudEvent BinaryEvent(byte[] data)
        {
            var e = new CloudEvent
            {
                Type = "com.example.test",
                Source = new Uri("https://example.com"),
                Id = Guid.NewGuid().ToString("N"),
                DataContentType = "application/octet-stream",
                Data = data
            };
            return e;
        }

        private static readonly EventSubscriptionContext Empty = EventSubscriptionContext.Empty;

        [Fact]
        public static void JsonPath_ExactMatch_TopLevel_ReturnsTrue()
        {
            var filter = EventFilter.ByField("status", FilterExpressionType.Equal, "active");
            Assert.True(filter.Matches(JsonEvent(new { status = "active" }), Empty));
        }

        [Fact]
        public static void JsonPath_ExactMatch_TopLevel_ReturnsFalse()
        {
            var filter = EventFilter.ByField("status", FilterExpressionType.Equal, "inactive");
            Assert.False(filter.Matches(JsonEvent(new { status = "active" }), Empty));
        }

        [Fact]
        public static void JsonPath_Nested_ReturnsTrue()
        {
            var filter = EventFilter.ByField("order.customer.tier", FilterExpressionType.Equal, "gold");
            Assert.True(filter.Matches(JsonEvent(new { order = new { customer = new { tier = "gold" } } }), Empty));
        }

        [Fact]
        public static void JsonPath_Nested_MissingSegment_ReturnsFalse()
        {
            var filter = EventFilter.ByField("order.customer.tier", FilterExpressionType.Equal, "gold");
            Assert.False(filter.Matches(JsonEvent(new { order = new { customer = new { } } }), Empty));
        }

        [Fact]
        public static void JsonPath_PrefixPattern_Matches()
        {
            var filter = EventFilter.FieldStartsWith("type", "order.");
            Assert.True(filter.Matches(JsonEvent(new { type = "order.placed" }), Empty));
        }

        [Fact]
        public static void JsonPath_PrefixPattern_NoMatch()
        {
            var filter = EventFilter.FieldStartsWith("type", "order.");
            Assert.False(filter.Matches(JsonEvent(new { type = "shipment.dispatched" }), Empty));
        }

        [Fact]
        public static void JsonPath_FromJsonElement_Matches()
        {
            using var doc = JsonDocument.Parse("""{"status":"active"}""");
            var @event = JsonEvent(doc.RootElement.Clone());
            var filter = EventFilter.ByField("status", FilterExpressionType.Equal, "active");
            Assert.True(filter.Matches(@event, Empty));
        }

        [Fact]
        public static void JsonPath_FromJsonString_Matches()
        {
            var @event = JsonEvent("""{"status":"active"}""");
            var filter = EventFilter.ByField("status", FilterExpressionType.Equal, "active");
            Assert.True(filter.Matches(@event, Empty));
        }

        [Fact]
        public static void JsonPath_BinaryData_ReturnsFalse()
        {
            var filter = EventFilter.ByField("status", FilterExpressionType.Equal, "active");
            Assert.False(filter.Matches(BinaryEvent([1, 2, 3]), Empty));
        }

        [Fact]
        public static void JsonPath_NullData_ReturnsFalse()
        {
            var filter = EventFilter.ByField("status", FilterExpressionType.Equal, "active");
            Assert.False(filter.Matches(JsonEvent(null), Empty));
        }

        [Fact]
        public static void JsonPath_NonJsonContentType_ReturnsFalse()
        {
            var filter = EventFilter.ByField("status", FilterExpressionType.Equal, "active");
            Assert.False(filter.Matches(JsonEvent(new { status = "active" }, contentType: "text/plain"), Empty));
        }

        [Fact]
        public static void DataFilter_CombinedWithTypeFilter_BothMustPass()
        {
            var filter = EventFilter.All(
                EventFilter.ByType("com.example.order.placed"),
                EventFilter.ByField("order.status", "confirmed"));

            var matching = JsonEvent(new { order = new { status = "confirmed" } });
            matching.Type = "com.example.order.placed";
            matching.Source = new Uri("https://example.com");

            var wrongBody = JsonEvent(new { order = new { status = "pending" } });
            wrongBody.Type = "com.example.order.placed";
            wrongBody.Source = new Uri("https://example.com");

            Assert.True(filter.Matches(matching, EventSubscriptionContext.Empty));
            Assert.False(filter.Matches(wrongBody, EventSubscriptionContext.Empty));
        }

        [Fact]
        public static async Task DataFilter_RoutesViaDispatcher_WhenBodyMatches()
        {
            var invoked = false;

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventPublisher(opt => opt.Source = new Uri("https://example.com"))
                .AddSubscriptions()
                .Subscribe(
                    EventFilter.All(
                        EventFilter.ByType("com.example.order.placed"),
                        EventFilter.ByField("tier", "gold")),
                    (_, _) => { invoked = true; return Task.CompletedTask; });

            var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<EventPublisher>()
                .UseDispatcher();

            await publisher.PublishEventAsync(new CloudEvent
            {
                Type = "com.example.order.placed",
                Source = new Uri("https://example.com"),
                Id = Guid.NewGuid().ToString("N"),
                Time = DateTimeOffset.UtcNow,
                DataContentType = "application/json",
                Data = new { tier = "gold" }
            });

            Assert.True(invoked);
        }

        [Fact]
        public static async Task DataFilter_DoesNotRouteWhenBodyDoesNotMatch()
        {
            var invoked = false;

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventPublisher(opt => opt.Source = new Uri("https://example.com"))
                .AddSubscriptions()
                .Subscribe(
                    EventFilter.All(
                        EventFilter.ByType("com.example.order.placed"),
                        EventFilter.ByField("tier", "gold")),
                    (_, _) => { invoked = true; return Task.CompletedTask; });

            var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<EventPublisher>()
                .UseDispatcher();

            await publisher.PublishEventAsync(new CloudEvent
            {
                Type = "com.example.order.placed",
                Source = new Uri("https://example.com"),
                Id = Guid.NewGuid().ToString("N"),
                Time = DateTimeOffset.UtcNow,
                DataContentType = "application/json",
                Data = new { tier = "silver" }
            });

            Assert.False(invoked);
        }
    }
}




