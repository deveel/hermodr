//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Text.Json;
using System.Text.Json.Serialization;

using CloudNative.CloudEvents;

using Microsoft.Extensions.DependencyInjection;

namespace Deveel.Events
{
    [Trait("Feature", "Subscriptions")]
    public static class TypedEventDataFilterTests
    {
        // ── Test model ────────────────────────────────────────────────────────────────

        private sealed record OrderEvent(
            [property: JsonPropertyName("orderId")] string OrderId,
            [property: JsonPropertyName("tier")] string Tier,
            [property: JsonPropertyName("amount")] decimal Amount,
            [property: JsonPropertyName("confirmed")] bool Confirmed);

        // ── Helpers ───────────────────────────────────────────────────────────────────

        private static CloudEvent JsonCloudEvent(object data, string type = "com.example.order.placed")
            => new()
            {
                Type = type,
                Source = new Uri("https://example.com"),
                Id = Guid.NewGuid().ToString("N"),
                DataContentType = "application/json",
                Data = data
            };

        private static readonly EventSubscriptionContext Empty = EventSubscriptionContext.Empty;

        // ── TypedEventDataFilter<TEvent> direct tests ─────────────────────────────────

        [Fact]
        public static void Predicate_ExactStringField_Matches()
        {
            var filter = new TypedEventDataFilter<OrderEvent>(o => o.Tier == "gold");
            var @event = JsonCloudEvent(new { orderId = "1", tier = "gold", amount = 100, confirmed = true });
            Assert.True(filter.Matches(@event, Empty));
        }

        [Fact]
        public static void Predicate_ExactStringField_DoesNotMatch()
        {
            var filter = new TypedEventDataFilter<OrderEvent>(o => o.Tier == "gold");
            var @event = JsonCloudEvent(new { orderId = "1", tier = "silver", amount = 100, confirmed = true });
            Assert.False(filter.Matches(@event, Empty));
        }

        [Fact]
        public static void Predicate_NumericComparison_Matches()
        {
            var filter = new TypedEventDataFilter<OrderEvent>(o => o.Amount > 50);
            var @event = JsonCloudEvent(new { orderId = "1", tier = "gold", amount = 99.5m, confirmed = true });
            Assert.True(filter.Matches(@event, Empty));
        }

        [Fact]
        public static void Predicate_NumericComparison_DoesNotMatch()
        {
            var filter = new TypedEventDataFilter<OrderEvent>(o => o.Amount > 200);
            var @event = JsonCloudEvent(new { orderId = "1", tier = "gold", amount = 99.5m, confirmed = true });
            Assert.False(filter.Matches(@event, Empty));
        }

        [Fact]
        public static void Predicate_BooleanField_Matches()
        {
            var filter = new TypedEventDataFilter<OrderEvent>(o => o.Confirmed);
            var @event = JsonCloudEvent(new { orderId = "1", tier = "gold", amount = 10, confirmed = true });
            Assert.True(filter.Matches(@event, Empty));
        }

        [Fact]
        public static void Predicate_BooleanField_DoesNotMatch()
        {
            var filter = new TypedEventDataFilter<OrderEvent>(o => o.Confirmed);
            var @event = JsonCloudEvent(new { orderId = "1", tier = "gold", amount = 10, confirmed = false });
            Assert.False(filter.Matches(@event, Empty));
        }

        [Fact]
        public static void Predicate_CompoundExpression_Matches()
        {
            var filter = new TypedEventDataFilter<OrderEvent>(o => o.Tier == "gold" && o.Amount >= 100);
            var @event = JsonCloudEvent(new { orderId = "1", tier = "gold", amount = 150, confirmed = true });
            Assert.True(filter.Matches(@event, Empty));
        }

        [Fact]
        public static void Predicate_CompoundExpression_PartialFails()
        {
            var filter = new TypedEventDataFilter<OrderEvent>(o => o.Tier == "gold" && o.Amount >= 100);
            var @event = JsonCloudEvent(new { orderId = "1", tier = "gold", amount = 50, confirmed = true });
            Assert.False(filter.Matches(@event, Empty));
        }

        [Fact]
        public static void Predicate_NullEvent_ReturnsFalse()
        {
            var filter = new TypedEventDataFilter<OrderEvent>(o => o.Tier == "gold");
            Assert.False(filter.Matches(null!, Empty));
        }

        [Fact]
        public static void Predicate_NullData_ReturnsFalse()
        {
            var filter = new TypedEventDataFilter<OrderEvent>(o => o.Tier == "gold");
            var @event = JsonCloudEvent(null!);
            Assert.False(filter.Matches(@event, Empty));
        }

        [Fact]
        public static void Predicate_BinaryData_ReturnsFalse()
        {
            var filter = new TypedEventDataFilter<OrderEvent>(o => o.Tier == "gold");
            var @event = new CloudEvent
            {
                Type = "com.example.test",
                Source = new Uri("https://example.com"),
                Id = Guid.NewGuid().ToString("N"),
                DataContentType = "application/octet-stream",
                Data = new byte[] { 1, 2, 3 }
            };
            Assert.False(filter.Matches(@event, Empty));
        }

        [Fact]
        public static void Predicate_FromJsonString_Matches()
        {
            var filter = new TypedEventDataFilter<OrderEvent>(o => o.Tier == "gold");
            var @event = JsonCloudEvent("""{"orderId":"1","tier":"gold","amount":100,"confirmed":true}""");
            Assert.True(filter.Matches(@event, Empty));
        }

        [Fact]
        public static void Predicate_FromJsonElement_Matches()
        {
            var filter = new TypedEventDataFilter<OrderEvent>(o => o.Tier == "gold");
            using var doc = JsonDocument.Parse("""{"orderId":"1","tier":"gold","amount":100,"confirmed":true}""");
            var @event = JsonCloudEvent(doc.RootElement.Clone());
            Assert.True(filter.Matches(@event, Empty));
        }

        [Fact]
        public static void PredicateExpression_IsRetained()
        {
            System.Linq.Expressions.Expression<Func<OrderEvent, bool>> expr = o => o.Tier == "gold";
            var filter = new TypedEventDataFilter<OrderEvent>(expr);
            Assert.Same(expr, filter.Predicate);
        }

        // ── EventFilter.For<TEvent> factory method ────────────────────────────────────

        [Fact]
        public static void FactoryMethod_CreatesFilter()
        {
            var filter = EventFilter.For<OrderEvent>(o => o.Tier == "platinum");
            Assert.NotNull(filter);
            var @event = JsonCloudEvent(new { orderId = "2", tier = "platinum", amount = 500, confirmed = true });
            Assert.True(filter.Matches(@event, Empty));
        }

        // ── EventFilterBuilder.WithPredicate<TEvent> ──────────────────────────────────

        [Fact]
        public static void Builder_WithPredicate_Matches()
        {
            var filter = new EventFilterBuilder()
                .WithTypePattern("com.example.*")
                .WithPredicate<OrderEvent>(o => o.Confirmed && o.Amount > 0)
                .Build();

            var @event = JsonCloudEvent(new { orderId = "3", tier = "bronze", amount = 1, confirmed = true });
            Assert.True(filter.Matches(@event, Empty));
        }

        [Fact]
        public static void Builder_WithPredicate_DoesNotMatchWhenPredicateFails()
        {
            var filter = new EventFilterBuilder()
                .WithTypePattern("com.example.*")
                .WithPredicate<OrderEvent>(o => o.Confirmed && o.Amount > 0)
                .Build();

            var @event = JsonCloudEvent(new { orderId = "3", tier = "bronze", amount = 1, confirmed = false });
            Assert.False(filter.Matches(@event, Empty));
        }

        // ── Integration: dispatcher routes via typed predicate ────────────────────────

        [Fact]
        public static async Task Dispatcher_TypedPredicate_RoutesMatchingEvent()
        {
            var invoked = false;

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventPublisher(opt => opt.Source = new Uri("https://example.com"))
                .AddDispatcher()
                .Subscribe(
                    fb => fb.WithType("com.example.order.placed")
                             .WithPredicate<OrderEvent>(o => o.Tier == "gold"),
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
                Data = new { orderId = "42", tier = "gold", amount = 200, confirmed = true }
            });

            Assert.True(invoked);
        }

        [Fact]
        public static async Task Dispatcher_TypedPredicate_DoesNotRouteNonMatchingEvent()
        {
            var invoked = false;

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventPublisher(opt => opt.Source = new Uri("https://example.com"))
                .AddDispatcher()
                .Subscribe(
                    fb => fb.WithType("com.example.order.placed")
                             .WithPredicate<OrderEvent>(o => o.Tier == "gold"),
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
                Data = new { orderId = "43", tier = "silver", amount = 50, confirmed = true }
            });

            Assert.False(invoked);
        }
    }
}

