//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Text.Json;

using CloudNative.CloudEvents;

using Microsoft.Extensions.DependencyInjection;

namespace Deveel.Events
{
    /// <summary>
    /// Verifies that <see cref="EventDataFilter"/> and <see cref="LogicalEventFilter"/>
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

        // ── EventDataFilter — serialized CLR object (TryGetJsonElement path) ────────

        [Fact]
        public static void JsonPath_SerializedClrObject_TwoLevels_Matches()
        {
            var filter = EventDataFilter.Create("Customer.Tier", FilterOperator.Equals, "gold");
            Assert.True(filter.Matches(EventWithData(MakeOrder(tier: "gold")), EventSubscriptionContext.Empty));
        }

        [Fact]
        public static void JsonPath_SerializedClrObject_TwoLevels_NoMatch()
        {
            var filter = EventDataFilter.Create("Customer.Tier", FilterOperator.Equals, "gold");
            Assert.False(filter.Matches(EventWithData(MakeOrder(tier: "silver")), EventSubscriptionContext.Empty));
        }

        [Fact]
        public static void JsonPath_SerializedClrObject_ThreeLevels_Matches()
        {
            var filter = EventDataFilter.Create("Customer.Address.Country", FilterOperator.Equals, "US");
            Assert.True(filter.Matches(EventWithData(MakeOrder(country: "US")), EventSubscriptionContext.Empty));
        }

        [Fact]
        public static void JsonPath_SerializedClrObject_ThreeLevels_NoMatch()
        {
            var filter = EventDataFilter.Create("Customer.Address.Country", FilterOperator.Equals, "US");
            Assert.False(filter.Matches(EventWithData(MakeOrder(country: "DE")), EventSubscriptionContext.Empty));
        }

        [Fact]
        public static void JsonPath_SerializedClrObject_NumericLeaf_GreaterThan()
        {
            var filter = EventDataFilter.Create("Payment.Amount", FilterOperator.GreaterThan, 100.0);
            Assert.True(filter.Matches(EventWithData(MakeOrder(amount: 250)), EventSubscriptionContext.Empty));
            Assert.False(filter.Matches(EventWithData(MakeOrder(amount: 50)), EventSubscriptionContext.Empty));
        }

        [Fact]
        public static void JsonPath_SerializedClrObject_BooleanLeaf_Matches()
        {
            var filter = EventDataFilter.Create("Customer.IsVerified", FilterOperator.Equals, true);
            Assert.True(filter.Matches(EventWithData(MakeOrder(isVerified: true)), EventSubscriptionContext.Empty));
        }

        [Fact]
        public static void JsonPath_SerializedClrObject_BooleanLeaf_NoMatch()
        {
            var filter = EventDataFilter.Create("Customer.IsVerified", FilterOperator.Equals, true);
            Assert.False(filter.Matches(EventWithData(MakeOrder(isVerified: false)), EventSubscriptionContext.Empty));
        }

        [Fact]
        public static void JsonPath_SerializedClrObject_ArraySegment_ReturnsFalse()
        {
            // Tags is a List<string>; further navigation into it is not supported
            var filter = EventDataFilter.Create("Tags.0", FilterOperator.Equals, "vip");
            Assert.False(filter.Matches(EventWithData(MakeOrder()), EventSubscriptionContext.Empty));
        }

        [Fact]
        public static void JsonPath_SerializedClrObject_MissingTopLevel_ReturnsFalse()
        {
            var filter = EventDataFilter.Create("DoesNotExist.Tier", FilterOperator.Equals, "gold");
            Assert.False(filter.Matches(EventWithData(MakeOrder()), EventSubscriptionContext.Empty));
        }

        [Fact]
        public static void JsonPath_SerializedClrObject_PrefixPattern_Matches()
        {
            var filter = EventDataFilter.Create("Customer.Address.PostalCode", FilterOperator.StartsWith, "100");
            Assert.True(filter.Matches(EventWithData(MakeOrder()), EventSubscriptionContext.Empty));
        }

        // ── EventDataFilter — IEventFilter against serialized CLR objects ────────

        [Fact]
        public static void EventDataFilter_NestedPath_AgainstSerializedClrObject_Matches()
        {
            var filter = EventDataFilter.Create("Customer.Tier", FilterOperator.Equals, "gold");
            Assert.True(filter.Matches(EventWithData(MakeOrder(tier: "gold")), EventSubscriptionContext.Empty));
        }

        [Fact]
        public static void EventDataFilter_ThreeLevels_AgainstSerializedClrObject_Matches()
        {
            var filter = EventDataFilter.Create("Customer.Address.Country", FilterOperator.Equals, "US");
            Assert.True(filter.Matches(EventWithData(MakeOrder(country: "US")), EventSubscriptionContext.Empty));
            Assert.False(filter.Matches(EventWithData(MakeOrder(country: "FR")), EventSubscriptionContext.Empty));
        }

        [Fact]
        public static void EventDataFilter_AndNested_AgainstSerializedClrObject_Matches()
        {
            var filter = LogicalEventFilter.And(
                EventDataFilter.Create("Customer.Tier", FilterOperator.Equals, "gold"),
                EventDataFilter.Create("Payment.Amount", FilterOperator.GreaterThan, 100.0),
                EventDataFilter.Create("Customer.Address.Country", FilterOperator.Equals, "US"));

            Assert.True(filter.Matches(EventWithData(MakeOrder(tier: "gold", amount: 300, country: "US")), EventSubscriptionContext.Empty));
            Assert.False(filter.Matches(EventWithData(MakeOrder(tier: "gold", amount: 50, country: "US")), EventSubscriptionContext.Empty));
            Assert.False(filter.Matches(EventWithData(MakeOrder(tier: "gold", amount: 300, country: "DE")), EventSubscriptionContext.Empty));
        }

        [Fact]
        public static void EventDataFilter_Exists_NestedProperty_Matches()
        {
            var filter = EventDataFilter.Exists("Customer.Address");
            Assert.True(filter.Matches(EventWithData(MakeOrder()), EventSubscriptionContext.Empty));
        }

        [Fact]
        public static void EventDataFilter_NotExists_MissingNestedProperty_Matches()
        {
            var filter = EventDataFilter.NotExists("Customer.LoyaltyPoints");
            Assert.True(filter.Matches(EventWithData(MakeOrder()), EventSubscriptionContext.Empty));
        }

        // ── EventFilterBuilder — WithField / WithField on nested path

        [Fact]
        public static void Builder_WithJsonPath_Nested_Matches()
        {
            var subscription = new EventFilterBuilder()
                .WithType("com.example.order.placed")
                .WithField("Customer.Tier", "gold")
                .Build();

            Assert.True(subscription.Matches(EventWithData(MakeOrder(tier: "gold")), EventSubscriptionContext.Empty));
            Assert.False(subscription.Matches(EventWithData(MakeOrder(tier: "silver")), EventSubscriptionContext.Empty));
        }

        [Fact]
        public static void Builder_WithField_NestedAnd_Matches()
        {
            var subscription = new EventFilterBuilder()
                .WithType("com.example.order.placed")
                .With(LogicalEventFilter.And(
                    EventDataFilter.Create("Customer.Tier", FilterOperator.Equals, "gold"),
                    EventDataFilter.Create("Customer.Address.Country", FilterOperator.Equals, "US")))
                .Build();

            Assert.True(subscription.Matches(EventWithData(MakeOrder(tier: "gold", country: "US")), EventSubscriptionContext.Empty));
            Assert.False(subscription.Matches(EventWithData(MakeOrder(tier: "gold", country: "DE")), EventSubscriptionContext.Empty));
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
                             .WithField("Customer.Tier", "gold"),
                    (e, _) => { goldDeliveries.Add(e.Id!); return Task.CompletedTask; },
                    name: "gold-handler")
                // subscription 2: US country
                .Subscribe(
                    fb => fb.WithType("com.example.order.placed")
                             .WithField("Customer.Address.Country", FilterOperator.Equals, "US"),
                    (e, _) => { usDeliveries.Add(e.Id!); return Task.CompletedTask; },
                    name: "us-handler");

            var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<IEventPublisher>();

            var goldUs   = EventWithData(MakeOrder(tier: "gold",   country: "US"));
            var silverUs = EventWithData(MakeOrder(tier: "silver", country: "US"));
            var goldDe   = EventWithData(MakeOrder(tier: "gold",   country: "DE"));
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
        public static async Task Dispatcher_NestedNumericComparison_RoutesCorrectly()
        {
            var matched = new List<double>();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventPublisher(opt => opt.Source = new Uri("https://example.com"))
                .AddDispatcher()
                .Subscribe(
                    fb => fb.WithType("com.example.order.placed")
                             .With(LogicalEventFilter.And(
                                 EventDataFilter.Create("Payment.Amount", FilterOperator.GreaterThanOrEqual, 500.0),
                                 EventDataFilter.Create("Payment.IsPaid", FilterOperator.Equals, true))),
                    (e, _) =>
                    {
                        if (e.Data is OrderEvent o) matched.Add(o.Payment.Amount);
                        return Task.CompletedTask;
                    });

            var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<IEventPublisher>();

            await publisher.PublishEventAsync(EventWithData(MakeOrder(amount: 600,  isPaid: true)));   // match
            await publisher.PublishEventAsync(EventWithData(MakeOrder(amount: 499,  isPaid: true)));   // amount too low
            await publisher.PublishEventAsync(EventWithData(MakeOrder(amount: 500,  isPaid: false)));  // not paid
            await publisher.PublishEventAsync(EventWithData(MakeOrder(amount: 1000, isPaid: true)));   // match

            Assert.Equal(2, matched.Count);
            Assert.Contains(600d,  matched);
            Assert.Contains(1000d, matched);
        }
    }
}

