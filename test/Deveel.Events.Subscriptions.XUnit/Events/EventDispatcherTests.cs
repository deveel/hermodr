//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Text.Json;

using CloudNative.CloudEvents;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Deveel.Events
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

        // ── FilterMatchMode / EventAttributeFilter ──────────────────────────────────

        [Fact]
        public static void ExactFilter_MatchesExactValue()
        {
            var f = EventAttributeFilter.Type("com.example.order.placed");
            Assert.True(f.Matches("com.example.order.placed"));
        }

        [Fact]
        public static void ExactFilter_DoesNotMatchDifferentValue()
        {
            var f = EventAttributeFilter.Type("com.example.order.placed");
            Assert.False(f.Matches("com.example.order.updated"));
        }

        [Fact]
        public static void ExactFilter_DoesNotMatchNull()
        {
            var f = EventAttributeFilter.Type("com.example.order.placed");
            Assert.False(f.Matches(null));
        }

        [Fact]
        public static void PrefixFilter_MatchesStartingValue()
        {
            var f = EventAttributeFilter.Type("com.example.", FilterMatchMode.Prefix);
            Assert.True(f.Matches("com.example.order.placed"));
        }

        [Fact]
        public static void PrefixFilter_StripTrailingAsterisk()
        {
            var f = EventAttributeFilter.For("type", "com.example.*", parseWildcard: true);
            // The trailing * is stripped; value stored is "com.example."
            Assert.Equal("com.example.", f.Value);
            Assert.True(f.Matches("com.example.user.created"));
        }

        [Fact]
        public static void PrefixFilter_DoesNotMatchNonStartingValue()
        {
            var f = EventAttributeFilter.Type("com.example.", FilterMatchMode.Prefix);
            Assert.False(f.Matches("org.other.something"));
        }

        [Fact]
        public static void SuffixFilter_MatchesEndingValue()
        {
            var f = EventAttributeFilter.Type(".placed", FilterMatchMode.Suffix);
            Assert.True(f.Matches("com.example.order.placed"));
        }

        [Fact]
        public static void SuffixFilter_StripLeadingAsterisk()
        {
            var f = EventAttributeFilter.For("type", "*.placed", parseWildcard: true);
            Assert.Equal(".placed", f.Value);
            Assert.True(f.Matches("com.foobar.placed"));
        }

        [Fact]
        public static void ParsePattern_Exact()
        {
            var f = EventAttributeFilter.For("type", "com.example.order.placed", parseWildcard: true);
            Assert.Equal(FilterMatchMode.Exact, f.MatchMode);
        }

        [Fact]
        public static void ParsePattern_Prefix()
        {
            var f = EventAttributeFilter.For("type", "com.example.*", parseWildcard: true);
            Assert.Equal(FilterMatchMode.Prefix, f.MatchMode);
        }

        [Fact]
        public static void ParsePattern_Suffix()
        {
            var f = EventAttributeFilter.For("type", "*.placed", parseWildcard: true);
            Assert.Equal(FilterMatchMode.Suffix, f.MatchMode);
        }

        // ── EventFilterBuilder / IEventFilter.Matches ────────────────────────────────

        [Fact]
        public static void EmptyFilter_MatchesAnyEvent()
        {
            var filter = LogicalEventFilter.And();
            Assert.True(filter.Matches(MakeEvent(), EventSubscriptionContext.Empty));
        }

        [Fact]
        public static void TypeFilter_Exact_Matches()
        {
            var filter = EventAttributeFilter.Type("com.example.order.placed");
            Assert.True(filter.Matches(MakeEvent(type: "com.example.order.placed"), EventSubscriptionContext.Empty));
        }

        [Fact]
        public static void TypeFilter_Exact_DoesNotMatchOtherType()
        {
            var filter = EventAttributeFilter.Type("com.example.order.placed");
            Assert.False(filter.Matches(MakeEvent(type: "com.example.order.updated"), EventSubscriptionContext.Empty));
        }

        [Fact]
        public static void TypeFilter_Prefix_Wildcard_Matches()
        {
            var filter = EventAttributeFilter.Type("com.example.*", parseWildcard: true);
            Assert.True(filter.Matches(MakeEvent(type: "com.example.order.placed"), EventSubscriptionContext.Empty));
            Assert.True(filter.Matches(MakeEvent(type: "com.example.user.created"), EventSubscriptionContext.Empty));
        }

        [Fact]
        public static void TypeFilter_Prefix_Wildcard_DoesNotMatchOtherNamespace()
        {
            var filter = EventAttributeFilter.Type("com.example.*", parseWildcard: true);
            Assert.False(filter.Matches(MakeEvent(type: "org.other.event"), EventSubscriptionContext.Empty));
        }

        [Fact]
        public static void SourceFilter_Matches()
        {
            var filter = new EventFilterBuilder()
                .WithSource("https://example.com/api")
                .Build();
            Assert.True(filter.Matches(MakeEvent(source: "https://example.com/api"), EventSubscriptionContext.Empty));
        }

        [Fact]
        public static void SourceFilter_DoesNotMatchOtherSource()
        {
            var filter = new EventFilterBuilder()
                .WithSource("https://example.com/api")
                .Build();
            Assert.False(filter.Matches(MakeEvent(source: "https://other.com/api"), EventSubscriptionContext.Empty));
        }

        [Fact]
        public static void SubjectFilter_Matches()
        {
            var filter = new EventFilterBuilder()
                .WithSubject("orders/42")
                .Build();
            Assert.True(filter.Matches(MakeEvent(subject: "orders/42"), EventSubscriptionContext.Empty));
        }

        [Fact]
        public static void SubjectFilter_DoesNotMatchMissingSubject()
        {
            var filter = new EventFilterBuilder()
                .WithSubject("orders/42")
                .Build();
            // Event has no subject → subject is null → filter rejects
            Assert.False(filter.Matches(MakeEvent(), EventSubscriptionContext.Empty));
        }

        
        [Fact]
        public static void CombinedFilter_AllCriteriaMustPass()
        {
            var filter = new EventFilterBuilder()
                .WithTypePattern("com.example.*")
                .WithSource("https://example.com/api")
                .WithSubjectPattern("orders/*")
                .Build();

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
            var filter = LogicalEventFilter.And();
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
                EventAttributeFilter.Type("com.example.test"),
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
                EventAttributeFilter.Type("com.example.other"),
                (_, _) => Task.CompletedTask);

            registry.Register(sub);

            var matches = registry.GetMatchingSubscriptions(MakeEvent("com.example.test"));
            Assert.Empty(matches);
        }

        [Fact]
        public static void Constructor_AcceptsPreSeededSubscriptions()
        {
            var sub = new EventSubscription(
                LogicalEventFilter.And(),
                (_, _) => Task.CompletedTask);

            var registry = new EventSubscriptionRegistry([sub]);
            var matches = registry.GetMatchingSubscriptions(MakeEvent());
            Assert.Single(matches);
        }

        [Fact]
        public static void MultipleSubscriptions_AllMatchingAreReturned()
        {
            var registry = new EventSubscriptionRegistry();

            var sub1 = new EventSubscription(EventAttributeFilter.Type("com.example.*", parseWildcard: true),
                (_, _) => Task.CompletedTask, "sub1");
            var sub2 = new EventSubscription(EventAttributeFilter.Type("com.example.test"),
                (_, _) => Task.CompletedTask, "sub2");
            var sub3 = new EventSubscription(EventAttributeFilter.Type("com.example.other"),
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
        public async Task DispatchAsync_InvokesMatchingHandler()
        {
            var handled = new List<CloudEvent>();

            var registry = new EventSubscriptionRegistry([
                new EventSubscription(
                    EventAttributeFilter.Type("com.example.test"),
                    (e, _) => { handled.Add(e); return Task.CompletedTask; },
                    "sub")
            ]);

            var dispatcher = new EventDispatcher(registry);
            var @event = MakeEvent();
            await dispatcher.DispatchAsync(@event);

            Assert.Single(handled);
            Assert.Same(@event, handled[0]);
        }

        [Fact]
        public async Task DispatchAsync_DoesNotInvokeNonMatchingHandler()
        {
            var handled = false;

            var registry = new EventSubscriptionRegistry([
                new EventSubscription(
                    EventAttributeFilter.Type("com.example.other"),
                    (_, _) => { handled = true; return Task.CompletedTask; })
            ]);

            var dispatcher = new EventDispatcher(registry);
            await dispatcher.DispatchAsync(MakeEvent("com.example.test"));

            Assert.False(handled);
        }

        [Fact]
        public async Task DispatchAsync_DefaultOptions_DoesNotThrowOnHandlerError()
        {
            var registry = new EventSubscriptionRegistry([
                new EventSubscription(
                    LogicalEventFilter.And(),
                    (_, _) => throw new InvalidOperationException("boom"))
            ]);

            var dispatcher = new EventDispatcher(registry);

            // Should NOT throw by default.
            await dispatcher.DispatchAsync(MakeEvent());
        }

        [Fact]
        public async Task DispatchAsync_ThrowOnHandlerError_PropagatesException()
        {
            var registry = new EventSubscriptionRegistry([
                new EventSubscription(
                    LogicalEventFilter.And(),
                    (_, _) => throw new InvalidOperationException("boom"))
            ]);

            var dispatcher = new EventDispatcher(registry, options: new EventDispatcherOptions { ThrowOnHandlerError = true });

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                dispatcher.DispatchAsync(MakeEvent()));
        }

        [Fact]
        public async Task DispatchAsync_MultipleSubscribers_AllInvoked()
        {
            var invoked = new List<string>();

            var registry = new EventSubscriptionRegistry([
                new EventSubscription(
                    LogicalEventFilter.And(),
                    (_, _) => { invoked.Add("sub1"); return Task.CompletedTask; },
                    "sub1"),
                new EventSubscription(
                    LogicalEventFilter.And(),
                    (_, _) => { invoked.Add("sub2"); return Task.CompletedTask; },
                    "sub2")
            ]);

            var dispatcher = new EventDispatcher(registry);
            await dispatcher.DispatchAsync(MakeEvent());

            Assert.Equal(["sub1", "sub2"], invoked);
        }

        [Fact]
        public async Task AsPublishChannel_DispatchesEvent()
        {
            var handled = false;

            var registry = new EventSubscriptionRegistry([
                new EventSubscription(
                    LogicalEventFilter.And(),
                    (_, _) => { handled = true; return Task.CompletedTask; })
            ]);

            IEventPublishChannel channel = new EventDispatcher(registry);
            await channel.PublishAsync(MakeEvent());

            Assert.True(handled);
        }

        [Fact]
        public async Task DispatchAsync_RespectsCancellation()
        {
            var registry = new EventSubscriptionRegistry([
                new EventSubscription(LogicalEventFilter.And(),
                    async (_, ct) => await Task.Delay(Timeout.Infinite, ct))
            ]);

            using var cts = new CancellationTokenSource();
            var dispatcher = new EventDispatcher(registry);

            var task = dispatcher.DispatchAsync(MakeEvent(), cts.Token);
            await Task.Delay(50);
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
        public static void AddDispatcher_RegistersRequiredServices()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventPublisher()
                    .AddDispatcher();

            var provider = services.BuildServiceProvider();

            Assert.NotNull(provider.GetService<IEventSubscriptionRegistry>());
            Assert.NotNull(provider.GetService<IEventSubscriptionResolver>());
            Assert.NotNull(provider.GetService<IEventDispatcher>());
            Assert.NotNull(provider.GetService<EventDispatcher>());
        }

        [Fact]
        public static async Task AddDispatcher_WithInlineSubscription_RoutesEvent()
        {
            var handled = new List<string>();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventPublisher(opt =>
                {
                    opt.Source = new Uri("https://example.com");
                    opt.ThrowOnErrors = true;
                })
                .AddDispatcher()
                .Subscribe("com.example.test",
                    (e, _) => { handled.Add(e.Type!); return Task.CompletedTask; },
                    name: "test-handler");

            var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<IEventPublisher>();

            var @event = MakeEvent();
            @event.Time = DateTimeOffset.UtcNow;

            await publisher.PublishEventAsync(@event);

            Assert.Single(handled);
            Assert.Equal("com.example.test", handled[0]);
        }

        [Fact]
        public static async Task AddDispatcher_WithPatternSubscription_RoutesMatchingEvents()
        {
            var handled = new List<string>();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventPublisher(opt => opt.Source = new Uri("https://example.com"))
                .AddDispatcher()
                .Subscribe("com.example.*",
                    (e, _) => { handled.Add(e.Type!); return Task.CompletedTask; });

            var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<IEventPublisher>();

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
        public static async Task AddDispatcher_WithFilterBuilder_RoutesMatchingEvents()
        {
            var handled = false;

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventPublisher(opt => opt.Source = new Uri("https://example.com"))
                .AddDispatcher()
                .Subscribe(
                    fb => fb.WithTypePattern("com.example.*")
                             // Uri.ToString() normalises "https://example.com" → "https://example.com/"
                             .WithSource("https://example.com/"),
                    (_, _) => { handled = true; return Task.CompletedTask; });

            var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<IEventPublisher>();

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
        public static async Task AddDispatcher_WithHandlerClass_RoutesEvents()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventPublisher(opt => opt.Source = new Uri("https://example.com"))
                .AddDispatcher()
                .Subscribe<OrderPlacedSubscription>();

            var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<IEventPublisher>();
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

            public IEventFilter Filter =>
                EventAttributeFilter.Type("com.example.order.placed");

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
        // ── helpers ────────────────────────────────────────────────────────────────

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

        // ── EventDataFilter ───────────────────────────────────────────────────────

        [Fact]
        public static void JsonPath_ExactMatch_TopLevel_ReturnsTrue()
        {
            var filter = EventDataFilter.Create("status", FilterOperator.Equals, "active");
            var @event = JsonEvent(new { status = "active" });
            Assert.True(filter.Matches(@event, Empty));
        }

        [Fact]
        public static void JsonPath_ExactMatch_TopLevel_ReturnsFalse()
        {
            var filter = EventDataFilter.Create("status", FilterOperator.Equals, "inactive");
            var @event = JsonEvent(new { status = "active" });
            Assert.False(filter.Matches(@event, Empty));
        }

        [Fact]
        public static void JsonPath_Nested_ReturnsTrue()
        {
            var filter = EventDataFilter.Create("order.customer.tier", FilterOperator.Equals, "gold");
            var @event = JsonEvent(new { order = new { customer = new { tier = "gold" } } });
            Assert.True(filter.Matches(@event, Empty));
        }

        [Fact]
        public static void JsonPath_Nested_MissingSegment_ReturnsFalse()
        {
            var filter = EventDataFilter.Create("order.customer.tier", FilterOperator.Equals, "gold");
            var @event = JsonEvent(new { order = new { customer = new { } } });
            Assert.False(filter.Matches(@event, Empty));
        }

        [Fact]
        public static void JsonPath_PrefixPattern_Matches()
        {
            var filter = EventDataFilter.Create("type", FilterOperator.StartsWith, "order.");
            var @event = JsonEvent(new { type = "order.placed" });
            Assert.True(filter.Matches(@event, Empty));
        }

        [Fact]
        public static void JsonPath_PrefixPattern_NoMatch()
        {
            var filter = EventDataFilter.Create("type", FilterOperator.StartsWith, "order.");
            var @event = JsonEvent(new { type = "shipment.dispatched" });
            Assert.False(filter.Matches(@event, Empty));
        }

        [Fact]
        public static void JsonPath_FromJsonElement_Matches()
        {
            using var doc = JsonDocument.Parse("""{"status":"active"}""");
            var @event = JsonEvent(doc.RootElement.Clone());
            Assert.True(EventDataFilter.Create("status", FilterOperator.Equals, "active").Matches(@event, Empty));
        }

        [Fact]
        public static void JsonPath_FromJsonString_Matches()
        {
            var @event = JsonEvent("""{"status":"active"}""");
            Assert.True(EventDataFilter.Create("status", FilterOperator.Equals, "active").Matches(@event, Empty));
        }

        [Fact]
        public static void JsonPath_BinaryData_ReturnsFalse()
        {
            var filter = EventDataFilter.Create("status", FilterOperator.Equals, "active");
            Assert.False(filter.Matches(BinaryEvent([1, 2, 3]), Empty));
        }

        [Fact]
        public static void JsonPath_NullData_ReturnsFalse()
        {
            var filter = EventDataFilter.Create("status", FilterOperator.Equals, "active");
            Assert.False(filter.Matches(JsonEvent(null), Empty));
        }

        [Fact]
        public static void JsonPath_NonJsonContentType_ReturnsFalse()
        {
            var filter = EventDataFilter.Create("status", FilterOperator.Equals, "active");
            var @event = JsonEvent(new { status = "active" }, contentType: "text/plain");
            Assert.False(filter.Matches(@event, Empty));
        }
        

        // ── Integration with EventFilterBuilder ────────────────────────────────────────

        [Fact]
        public static void DataFilter_CombinedWithTypeFilter_BothMustPass()
        {
            var filter = new EventFilterBuilder()
                .WithType("com.example.order.placed")
                .WithField("order.status", "confirmed")
                .Build();

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
                .AddDispatcher()
                .Subscribe(
                    fb => fb.WithType("com.example.order.placed")
                             .WithField("tier", "gold"),
                    (_, _) => { invoked = true; return Task.CompletedTask; });

            var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<IEventPublisher>();

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
                .AddDispatcher()
                .Subscribe(
                    fb => fb.WithType("com.example.order.placed")
                             .WithField("tier", "gold"),
                    (_, _) => { invoked = true; return Task.CompletedTask; });

            var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<IEventPublisher>();

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

        // ── helper types ───────────────────────────────────────────────────────────
    }
}





