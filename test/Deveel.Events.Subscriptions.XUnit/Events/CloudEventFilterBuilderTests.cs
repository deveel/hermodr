//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Text.Json;

using CloudNative.CloudEvents;

using Deveel.Filters;

namespace Deveel.Events
{
    /// <summary>
    /// Tests that verify the <see cref="CloudEventFilterBuilder"/> fluent API produces
    /// correct <see cref="FilterExpression"/> values and that those expressions match
    /// (or reject) CloudEvents as expected.
    /// </summary>
    [Trait("Feature", "Subscriptions")]
    [Trait("Subject", "FilterBuilder")]
    public static class CloudEventFilterBuilderTests
    {
        // ── helpers ────────────────────────────────────────────────────────────────────

        private static CloudEvent MakeEvent(
            string type = "com.example.order.placed",
            string source = "https://example.com/api",
            string? subject = null,
            object? data = null)
        {
            var e = new CloudEvent
            {
                Type = type,
                Source = new Uri(source),
                Id = Guid.NewGuid().ToString("N"),
                Subject = subject,
                DataContentType = "application/json"
            };

            if (data is not null)
                e.Data = JsonSerializer.Serialize(data);

            return e;
        }

        private static bool Matches(FilterExpression filter, CloudEvent @event)
            => filter.Matches(@event, EventSubscriptionContext.Empty);

        // ── Build returns Empty when no conditions ─────────────────────────────────────

        [Fact]
        public static void Build_NoConditions_ReturnsEmptyFilter()
        {
            var filter = CloudEventFilter.New().Build();
            Assert.True(filter.IsEmpty);
        }

        // ── Envelope attribute conditions ──────────────────────────────────────────────

        [Fact]
        public static void ByType_ExactMatch_Matches()
        {
            var filter = CloudEventFilter.New()
                .ByType("com.example.order.placed")
                .Build();

            Assert.True(Matches(filter, MakeEvent("com.example.order.placed")));
        }

        [Fact]
        public static void ByType_ExactMatch_DoesNotMatchOtherType()
        {
            var filter = CloudEventFilter.New()
                .ByType("com.example.order.placed")
                .Build();

            Assert.False(Matches(filter, MakeEvent("com.example.order.updated")));
        }

        [Fact]
        public static void ByTypePattern_Prefix_MatchesCorrectPrefix()
        {
            var filter = CloudEventFilter.New()
                .ByTypePattern("com.example.*")
                .Build();

            Assert.True(Matches(filter, MakeEvent("com.example.order.placed")));
            Assert.False(Matches(filter, MakeEvent("com.other.event")));
        }

        [Fact]
        public static void ByTypePattern_Suffix_MatchesCorrectSuffix()
        {
            var filter = CloudEventFilter.New()
                .ByTypePattern("*.placed")
                .Build();

            Assert.True(Matches(filter, MakeEvent("com.example.order.placed")));
            Assert.False(Matches(filter, MakeEvent("com.example.order.updated")));
        }

        [Fact]
        public static void BySource_ExactMatch_Matches()
        {
            var filter = CloudEventFilter.New()
                .BySource("https://example.com/api")
                .Build();

            Assert.True(Matches(filter, MakeEvent(source: "https://example.com/api")));
            Assert.False(Matches(filter, MakeEvent(source: "https://other.com/api")));
        }

        [Fact]
        public static void BySourcePattern_Prefix_Matches()
        {
            var filter = CloudEventFilter.New()
                .BySourcePattern("https://example.*")
                .Build();

            Assert.True(Matches(filter, MakeEvent(source: "https://example.com/api")));
        }

        [Fact]
        public static void BySubject_ExactMatch_Matches()
        {
            var filter = CloudEventFilter.New()
                .BySubject("order-123")
                .Build();

            Assert.True(Matches(filter, MakeEvent(subject: "order-123")));
            Assert.False(Matches(filter, MakeEvent(subject: "order-999")));
        }

        [Fact]
        public static void BySubjectPattern_Prefix_Matches()
        {
            var filter = CloudEventFilter.New()
                .BySubjectPattern("order-*")
                .Build();

            Assert.True(Matches(filter, MakeEvent(subject: "order-123")));
            Assert.False(Matches(filter, MakeEvent(subject: "invoice-123")));
        }

        // ── Data field conditions ──────────────────────────────────────────────────────

        [Fact]
        public static void WithField_ExactString_Matches()
        {
            var filter = CloudEventFilter.New()
                .WithField("status", "active")
                .Build();

            Assert.True(Matches(filter, MakeEvent(data: new { status = "active" })));
            Assert.False(Matches(filter, MakeEvent(data: new { status = "inactive" })));
        }

        [Fact]
        public static void WithField_NumericGreaterThan_Matches()
        {
            var filter = CloudEventFilter.New()
                .WithField("amount", FilterExpressionType.GreaterThan, 100)
                .Build();

            Assert.True(Matches(filter, MakeEvent(data: new { amount = 200 })));
            Assert.False(Matches(filter, MakeEvent(data: new { amount = 50 })));
        }

        [Fact]
        public static void FieldStartsWith_Matches()
        {
            var filter = CloudEventFilter.New()
                .FieldStartsWith("customerId", "cust-")
                .Build();

            Assert.True(Matches(filter, MakeEvent(data: new { customerId = "cust-42" })));
            Assert.False(Matches(filter, MakeEvent(data: new { customerId = "user-42" })));
        }

        [Fact]
        public static void FieldEndsWith_Matches()
        {
            var filter = CloudEventFilter.New()
                .FieldEndsWith("email", "@example.com")
                .Build();

            Assert.True(Matches(filter, MakeEvent(data: new { email = "alice@example.com" })));
            Assert.False(Matches(filter, MakeEvent(data: new { email = "alice@other.com" })));
        }

        [Fact]
        public static void FieldContains_Matches()
        {
            var filter = CloudEventFilter.New()
                .FieldContains("notes", "urgent")
                .Build();

            Assert.True(Matches(filter, MakeEvent(data: new { notes = "this is urgent" })));
            Assert.False(Matches(filter, MakeEvent(data: new { notes = "routine request" })));
        }

        [Fact]
        public static void FieldExists_Matches_WhenFieldPresent()
        {
            var filter = CloudEventFilter.New()
                .FieldExists("customerId")
                .Build();

            Assert.True(Matches(filter, MakeEvent(data: new { customerId = "cust-1" })));
            Assert.False(Matches(filter, MakeEvent(data: new { orderId = "ord-1" })));
        }

        [Fact]
        public static void FieldNotExists_Matches_WhenFieldAbsent()
        {
            var filter = CloudEventFilter.New()
                .FieldNotExists("deletedAt")
                .Build();

            Assert.True(Matches(filter, MakeEvent(data: new { status = "active" })));
            Assert.False(Matches(filter, MakeEvent(data: new { deletedAt = "2024-01-01" })));
        }

        // ── Multiple top-level conditions are AND-ed ───────────────────────────────────

        [Fact]
        public static void MultipleConditions_AllMustMatch()
        {
            var filter = CloudEventFilter.New()
                .ByType("com.example.order.placed")
                .BySource("https://example.com/api")
                .WithField("status", "active")
                .Build();

            // All three match → true
            Assert.True(Matches(filter,
                MakeEvent("com.example.order.placed", "https://example.com/api",
                    data: new { status = "active" })));

            // Wrong type → false
            Assert.False(Matches(filter,
                MakeEvent("com.example.order.updated", "https://example.com/api",
                    data: new { status = "active" })));

            // Wrong source → false
            Assert.False(Matches(filter,
                MakeEvent("com.example.order.placed", "https://other.com/api",
                    data: new { status = "active" })));

            // Wrong field value → false
            Assert.False(Matches(filter,
                MakeEvent("com.example.order.placed", "https://example.com/api",
                    data: new { status = "inactive" })));
        }

        // ── AnyOf ──────────────────────────────────────────────────────────────────────

        [Fact]
        public static void AnyOf_EitherConditionMatches()
        {
            var filter = CloudEventFilter.New()
                .AnyOf(b => b
                    .ByType("com.example.order.placed")
                    .ByType("com.example.order.updated"))
                .Build();

            Assert.True(Matches(filter, MakeEvent("com.example.order.placed")));
            Assert.True(Matches(filter, MakeEvent("com.example.order.updated")));
            Assert.False(Matches(filter, MakeEvent("com.example.order.deleted")));
        }

        [Fact]
        public static void AnyOf_EmptyInner_ThrowsInvalidOperationException()
        {
            Assert.Throws<InvalidOperationException>(() =>
                CloudEventFilter.New()
                    .AnyOf(_ => { })
                    .Build());
        }

        // ── AllOf ──────────────────────────────────────────────────────────────────────

        [Fact]
        public static void AllOf_AllConditionsMustMatch()
        {
            var filter = CloudEventFilter.New()
                .AllOf(b => b
                    .ByType("com.example.order.placed")
                    .BySource("https://example.com/api"))
                .Build();

            Assert.True(Matches(filter,
                MakeEvent("com.example.order.placed", "https://example.com/api")));
            Assert.False(Matches(filter,
                MakeEvent("com.example.order.placed", "https://other.com/api")));
        }

        [Fact]
        public static void AllOf_EmptyInner_ThrowsInvalidOperationException()
        {
            Assert.Throws<InvalidOperationException>(() =>
                CloudEventFilter.New()
                    .AllOf(_ => { })
                    .Build());
        }

        // ── Not ────────────────────────────────────────────────────────────────────────

        [Fact]
        public static void Not_NegatesSingleCondition()
        {
            var filter = CloudEventFilter.New()
                .Not(b => b.ByType("com.example.order.deleted"))
                .Build();

            Assert.True(Matches(filter, MakeEvent("com.example.order.placed")));
            Assert.False(Matches(filter, MakeEvent("com.example.order.deleted")));
        }

        [Fact]
        public static void Not_EmptyInner_ThrowsInvalidOperationException()
        {
            Assert.Throws<InvalidOperationException>(() =>
                CloudEventFilter.New()
                    .Not(_ => { })
                    .Build());
        }

        // ── Combining AnyOf inside a top-level AND ─────────────────────────────────────

        [Fact]
        public static void AnyOf_CombinedWithTopLevelAnd_BothMustHold()
        {
            var filter = CloudEventFilter.New()
                .BySource("https://example.com/api")
                .AnyOf(b => b
                    .ByType("com.example.order.placed")
                    .ByType("com.example.order.updated"))
                .Build();

            // matching source AND matching type
            Assert.True(Matches(filter,
                MakeEvent("com.example.order.placed", "https://example.com/api")));
            Assert.True(Matches(filter,
                MakeEvent("com.example.order.updated", "https://example.com/api")));

            // wrong source
            Assert.False(Matches(filter,
                MakeEvent("com.example.order.placed", "https://other.com/api")));

            // wrong type
            Assert.False(Matches(filter,
                MakeEvent("com.example.order.deleted", "https://example.com/api")));
        }
    }
}







