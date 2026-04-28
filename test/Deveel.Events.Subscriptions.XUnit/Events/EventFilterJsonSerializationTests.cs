//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Text.Json;
using System.Text.Json.Serialization;

using CloudNative.CloudEvents;

namespace Deveel.Events
{
    [Trait("Feature", "Subscriptions")]
    public static class EventFilterJsonSerializationTests
    {
        // ── Round-trip helpers ────────────────────────────────────────────────────────

        private static EventFilter RoundTrip(EventFilter filter)
        {
            var json = filter.ToJson();
            return EventFilter.FromJson(json);
        }

        private static void AssertMatches(EventFilter restored, CloudEvent e)
            => Assert.True(restored.Matches(e, EventSubscriptionContext.Empty));

        private static void AssertDoesNotMatch(EventFilter restored, CloudEvent e)
            => Assert.False(restored.Matches(e, EventSubscriptionContext.Empty));

        private static CloudEvent MakeEvent(
            string type = "com.example.order.placed",
            string source = "https://example.com",
            string? subject = null,
            object? data = null,
            string contentType = "application/json")
        {
            var e = new CloudEvent
            {
                Type = type,
                Source = new Uri(source),
                Id = Guid.NewGuid().ToString("N"),
                Subject = subject,
                DataContentType = data is not null ? contentType : null,
                Data = data
            };
            return e;
        }

        // ── EventAttributeFilter ──────────────────────────────────────────────────────

        [Fact]
        public static void AttributeFilter_Exact_RoundTrips()
        {
            var original = EventFilter.Type("com.example.order.placed");
            var restored = (EventAttributeFilter)RoundTrip(original);

            Assert.Equal("type", restored.AttributeName);
            Assert.Equal("com.example.order.placed", restored.Value);
            Assert.Equal(FilterMatchMode.Exact, restored.MatchMode);
            AssertMatches(restored, MakeEvent(type: "com.example.order.placed"));
            AssertDoesNotMatch(restored, MakeEvent(type: "com.example.order.updated"));
        }

        [Fact]
        public static void AttributeFilter_Prefix_RoundTrips()
        {
            var original = EventFilter.Type("com.example.", FilterMatchMode.Prefix);
            var restored = (EventAttributeFilter)RoundTrip(original);

            Assert.Equal(FilterMatchMode.Prefix, restored.MatchMode);
            Assert.Equal("com.example.", restored.Value);
            AssertMatches(restored, MakeEvent(type: "com.example.order.placed"));
            AssertDoesNotMatch(restored, MakeEvent(type: "org.other.event"));
        }

        [Fact]
        public static void AttributeFilter_Suffix_RoundTrips()
        {
            var original = EventFilter.Type(".placed", FilterMatchMode.Suffix);
            var restored = (EventAttributeFilter)RoundTrip(original);

            Assert.Equal(FilterMatchMode.Suffix, restored.MatchMode);
            AssertMatches(restored, MakeEvent(type: "com.example.order.placed"));
            AssertDoesNotMatch(restored, MakeEvent(type: "com.example.order.cancelled"));
        }

        [Fact]
        public static void AttributeFilter_Source_RoundTrips()
        {
            var original = new EventAttributeFilter("source", "https://example.com/");
            var restored = (EventAttributeFilter)RoundTrip(original);

            Assert.Equal("source", restored.AttributeName);
            Assert.Equal("https://example.com/", restored.Value);
        }

        [Fact]
        public static void AttributeFilter_ExtensionAttribute_RoundTrips()
        {
            var original = EventFilter.ForExtension("tenantid", "acme");
            var restored = (EventAttributeFilter)RoundTrip(original);

            Assert.Equal("extension.tenantid", restored.AttributeName);
            Assert.Equal("acme", restored.Value);
        }

        // ── EventDataFilter ───────────────────────────────────────────────────────────

        [Fact]
        public static void DataFilter_StringEquals_RoundTrips()
        {
            var original = EventFilter.Create("order.status", FilterOperator.Equals, "confirmed");
            var restored = (EventDataFilter)RoundTrip(original);

            Assert.Equal("order.status", restored.Path);
            Assert.Equal(FilterOperator.Equals, restored.Operator);
            Assert.Equal("confirmed", restored.Value);
        }

        [Fact]
        public static void DataFilter_BoolEquals_RoundTrips()
        {
            var original = EventFilter.Create("confirmed", FilterOperator.Equals, true);
            var restored = (EventDataFilter)RoundTrip(original);

            Assert.Equal(true, restored.Value);
            AssertMatches(restored, MakeEvent(data: new { confirmed = true }));
        }

        [Fact]
        public static void DataFilter_IntGreaterThan_RoundTrips()
        {
            var original = EventFilter.Create("amount", FilterOperator.GreaterThan, 100);
            var restored = (EventDataFilter)RoundTrip(original);

            Assert.Equal(100, restored.Value);
            Assert.Equal(FilterOperator.GreaterThan, restored.Operator);
        }

        [Fact]
        public static void DataFilter_LongEquals_RoundTrips()
        {
            var original = EventFilter.Create("timestamp", FilterOperator.Equals, 1_700_000_000L);
            var restored = (EventDataFilter)RoundTrip(original);

            Assert.Equal(1_700_000_000L, restored.Value);
        }

        [Fact]
        public static void DataFilter_DoubleGreaterThan_RoundTrips()
        {
            var original = EventFilter.Create("score", FilterOperator.GreaterThan, 4.5);
            var restored = (EventDataFilter)RoundTrip(original);

            Assert.Equal(4.5, (double)restored.Value!);
        }

        [Fact]
        public static void DataFilter_DateTimeEquals_RoundTrips()
        {
            var dt = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc);
            var original = EventFilter.Create("createdAt", FilterOperator.Equals, dt);
            var restored = (EventDataFilter)RoundTrip(original);

            Assert.Equal(dt, (DateTime)restored.Value!);
        }

        [Fact]
        public static void DataFilter_DateTimeOffsetEquals_RoundTrips()
        {
            var dto = new DateTimeOffset(2024, 6, 1, 12, 0, 0, TimeSpan.FromHours(2));
            var original = EventFilter.Create("scheduledAt", FilterOperator.Equals, dto);
            var restored = (EventDataFilter)RoundTrip(original);

            Assert.Equal(dto, (DateTimeOffset)restored.Value!);
        }

        [Fact]
        public static void DataFilter_Exists_RoundTrips()
        {
            var original = EventFilter.Exists("metadata");
            var restored = (EventDataFilter)RoundTrip(original);

            Assert.Equal(FilterOperator.Exists, restored.Operator);
            Assert.Null(restored.Value);
        }

        [Fact]
        public static void DataFilter_NotExists_RoundTrips()
        {
            var original = EventFilter.NotExists("metadata");
            var restored = (EventDataFilter)RoundTrip(original);

            Assert.Equal(FilterOperator.NotExists, restored.Operator);
        }

        [Fact]
        public static void DataFilter_StartsWith_RoundTrips()
        {
            var original = EventFilter.Create("type", FilterOperator.StartsWith, "order.");
            var restored = (EventDataFilter)RoundTrip(original);

            Assert.Equal(FilterOperator.StartsWith, restored.Operator);
            Assert.Equal("order.", restored.Value);
            AssertMatches(restored, MakeEvent(data: new { type = "order.placed" }));
        }

        // ── LogicalEventFilter ────────────────────────────────────────────────────────

        [Fact]
        public static void LogicalFilter_And_EmptyChildren_RoundTrips()
        {
            var original = EventFilter.And();
            var restored = (LogicalEventFilter)RoundTrip(original);

            Assert.Equal(LogicalFilterOperator.And, restored.Kind);
            Assert.Empty(restored.Filters);
            // empty AND matches everything
            AssertMatches(restored, MakeEvent());
        }

        [Fact]
        public static void LogicalFilter_And_WithChildren_RoundTrips()
        {
            var original = EventFilter.And(
                EventFilter.Type("com.example.order.placed"),
                EventFilter.Create("tier", FilterOperator.Equals, "gold"));

            var restored = (LogicalEventFilter)RoundTrip(original);

            Assert.Equal(LogicalFilterOperator.And, restored.Kind);
            Assert.Equal(2, restored.Filters.Count);

            var matchingEvent = MakeEvent(
                type: "com.example.order.placed",
                data: new { tier = "gold" });
            AssertMatches(restored, matchingEvent);

            var wrongTypeEvent = MakeEvent(
                type: "com.example.order.updated",
                data: new { tier = "gold" });
            AssertDoesNotMatch(restored, wrongTypeEvent);
        }

        [Fact]
        public static void LogicalFilter_Or_RoundTrips()
        {
            var original = EventFilter.Or(
                EventFilter.Type("com.example.order.placed"),
                EventFilter.Type("com.example.order.updated"));

            var restored = (LogicalEventFilter)RoundTrip(original);

            Assert.Equal(LogicalFilterOperator.Or, restored.Kind);
            AssertMatches(restored, MakeEvent(type: "com.example.order.placed"));
            AssertMatches(restored, MakeEvent(type: "com.example.order.updated"));
            AssertDoesNotMatch(restored, MakeEvent(type: "org.other.event"));
        }

        [Fact]
        public static void LogicalFilter_Nested_RoundTrips()
        {
            var original = EventFilter.And(
                EventFilter.Or(
                    EventFilter.Type("com.example.order.placed"),
                    EventFilter.Type("com.example.order.updated")),
                EventFilter.Create("tier", FilterOperator.Equals, "gold"));

            var restored = (LogicalEventFilter)RoundTrip(original);
            Assert.Equal(LogicalFilterOperator.And, restored.Kind);
            Assert.Equal(2, restored.Filters.Count);
            Assert.IsType<LogicalEventFilter>(restored.Filters[0]);
        }

        // ── ToJson output structure ───────────────────────────────────────────────────

        [Fact]
        public static void AttributeFilter_ToJson_ContainsExpectedKeys()
        {
            var json = EventFilter.Type("com.example.test").ToJson();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.Equal("attribute", root.GetProperty("$filter").GetString());
            Assert.Equal("type", root.GetProperty("attribute").GetString());
            Assert.Equal("com.example.test", root.GetProperty("value").GetString());
            Assert.Equal("Exact", root.GetProperty("matchMode").GetString());
        }

        [Fact]
        public static void DataFilter_ToJson_ContainsExpectedKeys()
        {
            var json = EventFilter.Create("status", FilterOperator.Equals, "active").ToJson();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.Equal("data", root.GetProperty("$filter").GetString());
            Assert.Equal("status", root.GetProperty("path").GetString());
            Assert.Equal("Equals", root.GetProperty("operator").GetString());
            Assert.Equal("active", root.GetProperty("value").GetString());
            Assert.Equal("string", root.GetProperty("valueType").GetString());
        }

        [Fact]
        public static void LogicalFilter_ToJson_ContainsFiltersArray()
        {
            var json = EventFilter.And(EventFilter.Type("com.example.test")).ToJson();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.Equal("and", root.GetProperty("$filter").GetString());
            Assert.Equal(JsonValueKind.Array, root.GetProperty("filters").ValueKind);
            Assert.Equal(1, root.GetProperty("filters").GetArrayLength());
        }

        // ── TypedEventDataFilter throws ───────────────────────────────────────────────

        private sealed record OrderModel(
            [property: JsonPropertyName("tier")] string Tier);

        [Fact]
        public static void TypedFilter_ToJson_Throws()
        {
            var filter = EventFilter.For<OrderModel>(o => o.Tier == "gold");
            Assert.Throws<NotSupportedException>(() => filter.ToJson());
        }

        [Fact]
        public static void LogicalFilter_ContainingTyped_Throws()
        {
            var filter = EventFilter.And(
                EventFilter.Type("com.example.test"),
                EventFilter.For<OrderModel>(o => o.Tier == "gold"));

            Assert.Throws<NotSupportedException>(() => filter.ToJson());
        }

        // ── FromJson error handling ───────────────────────────────────────────────────

        [Fact]
        public static void FromJson_UnknownDiscriminator_Throws()
        {
            Assert.Throws<JsonException>(() =>
                EventFilter.FromJson("""{"$filter":"unknown","value":"x"}"""));
        }

        [Fact]
        public static void FromJson_MissingDiscriminator_Throws()
        {
            Assert.Throws<JsonException>(() =>
                EventFilter.FromJson("""{"value":"x"}"""));
        }

        [Fact]
        public static void FromJson_UnknownValueType_Throws()
        {
            Assert.Throws<JsonException>(() =>
                EventFilter.FromJson("""{"$filter":"data","path":"x","operator":"Equals","value":"v","valueType":"uuid"}"""));
        }
    }
}





