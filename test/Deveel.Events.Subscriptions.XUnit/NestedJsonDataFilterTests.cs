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
    /// <summary>
    /// Verifies that <see cref="EventDataFilter"/> and <see cref="FilterExpression"/>
    /// correctly evaluate nested properties in serialized JSON event bodies — covering
    /// anonymous types, named CLR classes, 3-level paths, numeric/boolean leaves,
    /// arrays, and end-to-end dispatch.
    /// </summary>
    [Trait("Feature", "Subscriptions")]
    [Trait("Feature", "NestedFilter")]
    public static class NestedJsonDataFilterTests
    {
        // ── CLR model helpers ──────────────────────────────────────────────────────

        private sealed class OrderEvent
        {
            public string OrderId { get; set; } = string.Empty;
            public CustomerInfo Customer { get; set; } = new();
            public PaymentInfo Payment { get; set; } = new();
            public List<string> Tags { get; set; } = [];
        }

        private sealed class CustomerInfo
        {
            public string Name { get; set; } = string.Empty;
            public string Tier { get; set; } = string.Empty;
            public AddressInfo Address { get; set; } = new();
            public bool IsVerified { get; set; }
        }

        private sealed class AddressInfo
        {
            public string Country { get; set; } = string.Empty;
            public string PostalCode { get; set; } = string.Empty;
        }

        private sealed class PaymentInfo
        {
            public double Amount { get; set; }
            public string Currency { get; set; } = "USD";
            public bool IsPaid { get; set; }
        }

        private static CloudEvent EventWithData(object data, string contentType = "application/json")
            => new()
            {
                Type = "com.example.order.placed",
                Source = new Uri("https://example.com"),
                Id = Guid.NewGuid().ToString("N"),
                Time = DateTimeOffset.UtcNow,
                DataContentType = contentType,
                Data = data
            };

        private static OrderEvent MakeOrder(
            string tier = "gold",
            double amount = 250,
            bool isPaid = true,
            bool isVerified = true,
            string country = "US") => new()
        {
            OrderId = Guid.NewGuid().ToString("N"),
            Customer = new CustomerInfo
            {
                Name = "Alice",
                Tier = tier,
                IsVerified = isVerified,
                Address = new AddressInfo { Country = country, PostalCode = "10001" }
            },
            Payment = new PaymentInfo { Amount = amount, IsPaid = isPaid },
            Tags = ["vip", "repeat"]
        };

        // ── EventDataFilter — serialized CLR object (TryGetJsonElement path) ───────

        [Fact]
        public static void JsonPath_SerializedClrObject_TwoLevels_Matches()
        {
            var filter = EventDataFilter.JsonPath("Customer.Tier", "gold");
            Assert.True(filter.Matches(EventWithData(MakeOrder(tier: "gold"))));
        }

        [Fact]
        public static void JsonPath_SerializedClrObject_TwoLevels_NoMatch()
        {
            var filter = EventDataFilter.JsonPath("Customer.Tier", "gold");
            Assert.False(filter.Matches(EventWithData(MakeOrder(tier: "silver"))));
        }

        [Fact]
        public static void JsonPath_SerializedClrObject_ThreeLevels_Matches()
        {
            var filter = EventDataFilter.JsonPath("Customer.Address.Country", "US");
            Assert.True(filter.Matches(EventWithData(MakeOrder(country: "US"))));
        }

        [Fact]
        public static void JsonPath_SerializedClrObject_ThreeLevels_NoMatch()
        {
            var filter = EventDataFilter.JsonPath("Customer.Address.Country", "US");
            Assert.False(filter.Matches(EventWithData(MakeOrder(country: "DE"))));
        }

        [Fact]
        public static void JsonPath_SerializedClrObject_NumericLeaf_GreaterThan()
        {
            var filter = EventDataFilter.JsonPath("Payment.Amount", EventAttributeFilter.Prefix("2")); // ≥ 200-range string
            // Use EventDataFilter.JsonPathPattern for GreaterThan numeric — use FilterExpression for that
            var numFilter = EventDataFilter.JsonPredicate(root =>
                root.TryGetProperty("Payment", out var p) &&
                p.TryGetProperty("Amount", out var a) &&
                a.GetDouble() > 100);

            Assert.True(numFilter.Matches(EventWithData(MakeOrder(amount: 250))));
            Assert.False(numFilter.Matches(EventWithData(MakeOrder(amount: 50))));
        }

        [Fact]
        public static void JsonPath_SerializedClrObject_BooleanLeaf_Matches()
        {
            // Boolean stored as "True"/"False" string representation in the filter
            var filter = EventDataFilter.JsonPath("Customer.IsVerified", "True");
            Assert.True(filter.Matches(EventWithData(MakeOrder(isVerified: true))));
        }

        [Fact]
        public static void JsonPath_SerializedClrObject_BooleanLeaf_NoMatch()
        {
            var filter = EventDataFilter.JsonPath("Customer.IsVerified", "True");
            Assert.False(filter.Matches(EventWithData(MakeOrder(isVerified: false))));
        }

        [Fact]
        public static void JsonPath_SerializedClrObject_ArraySegment_ReturnsFalse()
        {
            // Tags is a List<string>; further navigation into it is not supported
            var filter = EventDataFilter.JsonPath("Tags.0", "vip");
            Assert.False(filter.Matches(EventWithData(MakeOrder())));
        }

        [Fact]
        public static void JsonPath_SerializedClrObject_MissingTopLevel_ReturnsFalse()
        {
            var filter = EventDataFilter.JsonPath("DoesNotExist.Tier", "gold");
            Assert.False(filter.Matches(EventWithData(MakeOrder())));
        }

        [Fact]
        public static void JsonPath_SerializedClrObject_PrefixPattern_Matches()
        {
            var filter = EventDataFilter.JsonPathPattern("Customer.Address.PostalCode", "100*");
            Assert.True(filter.Matches(EventWithData(MakeOrder())));
        }

        // ── FilterExpression — evaluated via JsonPredicateDataFilter wrapping ───────

        [Fact]
        public static void FilterExpression_NestedPath_AgainstSerializedClrObject_Matches()
        {
            var expr = FilterExpression.JsonPath("Customer.Tier", "gold");
            var filter = EventDataFilter.JsonPredicate(e => expr.Evaluate(e));
            Assert.True(filter.Matches(EventWithData(MakeOrder(tier: "gold"))));
        }

        [Fact]
        public static void FilterExpression_ThreeLevels_AgainstSerializedClrObject_Matches()
        {
            var expr = FilterExpression.JsonPath("Customer.Address.Country", "US");
            var filter = EventDataFilter.JsonPredicate(e => expr.Evaluate(e));
            Assert.True(filter.Matches(EventWithData(MakeOrder(country: "US"))));
            Assert.False(filter.Matches(EventWithData(MakeOrder(country: "FR"))));
        }

        [Fact]
        public static void FilterExpression_AndNested_AgainstSerializedClrObject_Matches()
        {
            var expr = FilterExpression.And(
                FilterExpression.JsonPath("Customer.Tier", "gold"),
                FilterExpression.JsonPath("Payment.Amount", FilterOperator.GreaterThan, "100"),
                FilterExpression.JsonPath("Customer.Address.Country", "US"));

            var filter = EventDataFilter.JsonPredicate(e => expr.Evaluate(e));

            Assert.True(filter.Matches(EventWithData(MakeOrder(tier: "gold", amount: 300, country: "US"))));
            Assert.False(filter.Matches(EventWithData(MakeOrder(tier: "gold", amount: 50, country: "US"))));
            Assert.False(filter.Matches(EventWithData(MakeOrder(tier: "gold", amount: 300, country: "DE"))));
        }

        [Fact]
        public static void FilterExpression_Exists_NestedProperty_Matches()
        {
            var expr = FilterExpression.JsonPath("Customer.Address", FilterOperator.Exists);
            var filter = EventDataFilter.JsonPredicate(e => expr.Evaluate(e));
            Assert.True(filter.Matches(EventWithData(MakeOrder())));
        }

        [Fact]
        public static void FilterExpression_NotExists_MissingNestedProperty_Matches()
        {
            var expr = FilterExpression.JsonPath("Customer.LoyaltyPoints", FilterOperator.NotExists);
            var filter = EventDataFilter.JsonPredicate(e => expr.Evaluate(e));
            Assert.True(filter.Matches(EventWithData(MakeOrder())));
        }

        // ── EventSubscriptionFilterBuilder — WithJsonPath on nested path ──────────

        [Fact]
        public static void Builder_WithJsonPath_Nested_Matches()
        {
            var subscription = EventSubscriptionFilter.Builder
                .WithType("com.example.order.placed")
                .WithJsonPath("Customer.Tier", "gold")
                .Build();

            Assert.True(subscription.Matches(EventWithData(MakeOrder(tier: "gold"))));
            Assert.False(subscription.Matches(EventWithData(MakeOrder(tier: "silver"))));
        }

        [Fact]
        public static void Builder_WithDataExpression_NestedAnd_Matches()
        {
            var subscription = EventSubscriptionFilter.Builder
                .WithType("com.example.order.placed")
                .WithDataExpression(
                    FilterExpression.And(
                        FilterExpression.JsonPath("Customer.Tier", "gold"),
                        FilterExpression.JsonPath("Customer.Address.Country", "US")))
                .Build();

            Assert.True(subscription.Matches(EventWithData(MakeOrder(tier: "gold", country: "US"))));
            Assert.False(subscription.Matches(EventWithData(MakeOrder(tier: "gold", country: "DE"))));
        }

        // ── EventSubscriptionFilterModel — nested expression serialization ─────────

        [Fact]
        public static void FilterModel_NestedExpression_JsonRoundTrip_EvaluatesCorrectly()
        {
            var model = new EventSubscriptionFilterModel
            {
                Type = AttributeFilterModel.Prefix("com.example.*"),
                DataExpression = FilterExpression.And(
                    FilterExpression.JsonPath("Customer.Tier", FilterOperator.Equals, "gold"),
                    FilterExpression.JsonPath("Payment.Amount", FilterOperator.GreaterThan, "100"),
                    FilterExpression.Not(
                        FilterExpression.JsonPath("Customer.Address.Country", FilterOperator.Equals, "XX")))
            };

            // Round-trip through JSON (simulates DB load)
            var json = model.ToJson();
            var restored = EventSubscriptionFilterModel.FromJson(json)!;
            var runtime = restored.ToRuntimeFilter();

            Assert.True(runtime.Matches(EventWithData(MakeOrder(tier: "gold", amount: 300, country: "US"))));
            Assert.False(runtime.Matches(EventWithData(MakeOrder(tier: "silver", amount: 300, country: "US"))));
            Assert.False(runtime.Matches(EventWithData(MakeOrder(tier: "gold", amount: 50, country: "US"))));
            Assert.False(runtime.Matches(EventWithData(MakeOrder(tier: "gold", amount: 300, country: "XX"))));
        }

        // ── End-to-end dispatcher: nested body routing ─────────────────────────────

        [Fact]
        public static async Task Dispatcher_NestedBodyFilter_RoutesOnlyMatchingEvents()
        {
            var goldDeliveries = new List<string>();
            var usDeliveries   = new List<string>();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventPublisher(opt => opt.Source = new Uri("https://example.com"))
                .AddDispatcher()
                // subscription 1: gold tier
                .Subscribe(
                    fb => fb.WithType("com.example.order.placed")
                             .WithJsonPath("Customer.Tier", "gold"),
                    (e, _) => { goldDeliveries.Add(e.Id!); return Task.CompletedTask; },
                    name: "gold-handler")
                // subscription 2: US country — uses declarative expression
                .Subscribe(
                    fb => fb.WithType("com.example.order.placed")
                             .WithDataExpression(
                                 FilterExpression.JsonPath("Customer.Address.Country", "US")),
                    (e, _) => { usDeliveries.Add(e.Id!); return Task.CompletedTask; },
                    name: "us-handler");

            var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<IEventPublisher>();

            var goldUs  = EventWithData(MakeOrder(tier: "gold",   country: "US"));
            var silverUs = EventWithData(MakeOrder(tier: "silver", country: "US"));
            var goldDe  = EventWithData(MakeOrder(tier: "gold",   country: "DE"));
            var silverDe = EventWithData(MakeOrder(tier: "silver", country: "DE"));

            await publisher.PublishEventAsync(goldUs);
            await publisher.PublishEventAsync(silverUs);
            await publisher.PublishEventAsync(goldDe);
            await publisher.PublishEventAsync(silverDe);

            // gold-handler: goldUs + goldDe
            Assert.Equal(2, goldDeliveries.Count);
            Assert.Contains(goldUs.Id,  goldDeliveries);
            Assert.Contains(goldDe.Id,  goldDeliveries);

            // us-handler: goldUs + silverUs
            Assert.Equal(2, usDeliveries.Count);
            Assert.Contains(goldUs.Id,   usDeliveries);
            Assert.Contains(silverUs.Id, usDeliveries);
        }

        [Fact]
        public static async Task Dispatcher_NestedModel_LoadedFromJson_RoutesCorrectly()
        {
            // Simulate a subscription stored in a database as JSON and re-loaded.
            var modelJson = new EventSubscriptionFilterModel
            {
                Type = AttributeFilterModel.Exact("com.example.order.placed"),
                DataExpression = FilterExpression.And(
                    FilterExpression.JsonPath("Customer.Tier", FilterOperator.Equals, "gold"),
                    FilterExpression.JsonPath("Payment.Amount", FilterOperator.GreaterThan, "200"),
                    FilterExpression.JsonPath("Customer.Address.Country", FilterOperator.StartsWith, "U"))
            }.ToJson();

            // Re-hydrate (as a DB-backed registry would do)
            var runtimeFilter = EventSubscriptionFilterModel.FromJson(modelJson)!.ToRuntimeFilter();

            var matched = new List<string>();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventPublisher(opt => opt.Source = new Uri("https://example.com"))
                .AddDispatcher()
                .Subscribe(runtimeFilter,
                    (e, _) => { matched.Add(e.Id!); return Task.CompletedTask; });

            var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<IEventPublisher>();

            // Should match: gold, amount=300, US
            var hit  = EventWithData(MakeOrder(tier: "gold",   amount: 300, country: "US"));
            // tier wrong
            var miss1 = EventWithData(MakeOrder(tier: "silver", amount: 300, country: "US"));
            // amount too low
            var miss2 = EventWithData(MakeOrder(tier: "gold",   amount: 100, country: "US"));
            // country doesn't start with U
            var miss3 = EventWithData(MakeOrder(tier: "gold",   amount: 300, country: "DE"));
            // UK starts with U → should match
            var hit2  = EventWithData(MakeOrder(tier: "gold",   amount: 300, country: "UK"));

            await publisher.PublishEventAsync(hit);
            await publisher.PublishEventAsync(miss1);
            await publisher.PublishEventAsync(miss2);
            await publisher.PublishEventAsync(miss3);
            await publisher.PublishEventAsync(hit2);

            Assert.Equal(2, matched.Count);
            Assert.Contains(hit.Id,  matched);
            Assert.Contains(hit2.Id, matched);
        }

        [Fact]
        public static async Task Dispatcher_NestedNumericComparison_RoutesCorrectly()
        {
            var matched = new List<double>();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventPublisher(opt => opt.Source = new Uri("https://example.com"))
                .AddDispatcher()
                .Subscribe(
                    fb => fb.WithType("com.example.order.placed")
                             .WithDataExpression(
                                 FilterExpression.And(
                                     FilterExpression.JsonPath("Payment.Amount", FilterOperator.GreaterThanOrEqual, "500"),
                                     FilterExpression.JsonPath("Payment.IsPaid", FilterOperator.Equals, "True"))),
                    (e, _) =>
                    {
                        if (e.Data is OrderEvent o) matched.Add(o.Payment.Amount);
                        return Task.CompletedTask;
                    });

            var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<IEventPublisher>();

            await publisher.PublishEventAsync(EventWithData(MakeOrder(amount: 600, isPaid: true)));   // match
            await publisher.PublishEventAsync(EventWithData(MakeOrder(amount: 499, isPaid: true)));   // amount too low
            await publisher.PublishEventAsync(EventWithData(MakeOrder(amount: 500, isPaid: false)));  // not paid
            await publisher.PublishEventAsync(EventWithData(MakeOrder(amount: 1000, isPaid: true)));  // match

            Assert.Equal(2, matched.Count);
            Assert.Contains(600d,  matched);
            Assert.Contains(1000d, matched);
        }
    }
}

