//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Text.Json;
using System.Text.Json.Serialization;

using CloudNative.CloudEvents;

using Deveel.Filters;

namespace Hermodr
{
    /// <summary>
    /// Verifies the JSON serialization and deserialization behaviour of
    /// <see cref="FilterExpressionExtensions.ToJson"/> and
    /// <see cref="EventFilter.FromJson"/>, including:
    /// <list type="bullet">
    ///   <item>Basic round-trip correctness for all supported filter kinds.</item>
    ///   <item>Custom <see cref="JsonSerializerOptions"/> handling (converter auto-injection
    ///   and non-mutation of the caller's original options).</item>
    ///   <item>Guard-clause behaviour (null / empty / whitespace input, invalid JSON).</item>
    ///   <item>Property-level <c>[JsonConverter]</c> annotation on a DTO.</item>
    /// </list>
    /// </summary>
    [Trait("Feature", "Subscriptions")]
    [Trait("Subject", "Serialization")]
    public static class EventFilterSerializationTests
    {
        // ── Shared helpers ─────────────────────────────────────────────────────────────

        private static CloudEvent MakeEvent(
            string type = "com.example.order.placed",
            string source = "https://example.com/api",
            string? subject = null,
            string? extension = null,
            string? extensionValue = null,
            object? data = null)
        {
            var e = new CloudEvent
            {
                Type    = type,
                Source  = new Uri(source),
                Id      = Guid.NewGuid().ToString("N"),
                Subject = subject,
                DataContentType = "application/json"
            };

            if (extension is not null && extensionValue is not null)
                e[CloudEventAttribute.CreateExtension(extension,
                    CloudEventAttributeType.String)] = extensionValue;

            if (data is not null)
                e.Data = JsonSerializer.Serialize(data);

            return e;
        }

        private static bool Matches(FilterExpression filter, CloudEvent @event)
            => filter.Matches(@event, EventSubscriptionContext.Empty);

        // ── ToJson — basic output ──────────────────────────────────────────────────────

        [Fact]
        public static void ToJson_SimpleFilter_ReturnsNonEmptyString()
        {
            var filter = EventFilter.ByType("com.example.order.placed");
            var json   = filter.ToJson();

            Assert.NotNull(json);
            Assert.NotEmpty(json);
        }

        [Fact]
        public static void ToJson_SimpleFilter_ReturnsValidJson()
        {
            var filter = EventFilter.ByType("com.example.order.placed");
            var json   = filter.ToJson();

            // Must be parseable — no exception means valid JSON
            using var doc = JsonDocument.Parse(json);
            Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
        }

        [Fact]
        public static void ToJson_CompoundAndFilter_ReturnsValidJson()
        {
            var filter = EventFilter.All(
                EventFilter.ByTypePattern("com.example.order.*"),
                EventFilter.ByField("customer.tier", "gold"));

            using var doc = JsonDocument.Parse(filter.ToJson());
            Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
        }

        [Fact]
        public static void ToJson_CompoundOrFilter_ReturnsValidJson()
        {
            var filter = EventFilter.Any(
                EventFilter.ByField("status", "placed"),
                EventFilter.ByField("status", "confirmed"));

            using var doc = JsonDocument.Parse(filter.ToJson());
            Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
        }

        [Fact]
        public static void ToJson_FluentBuilderFilter_ReturnsValidJson()
        {
            var filter = EventFilter.New()
                .ByTypePattern("com.example.*")
                .ByExtension("tenantid", "acme")
                .WithField("customer.tier", "gold")
                .Build();

            using var doc = JsonDocument.Parse(filter.ToJson());
            Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
        }

        // ── ToJson — custom JsonSerializerOptions ──────────────────────────────────────

        [Fact]
        public static void ToJson_WriteIndentedOptions_ProducesIndentedOutput()
        {
            var filter = EventFilter.ByType("com.example.order.placed");
            var opts   = new JsonSerializerOptions { WriteIndented = true };

            var json = filter.ToJson(opts);

            // Indented JSON contains newlines
            Assert.Contains('\n', json);
        }

        [Fact]
        public static void ToJson_OptionsWithoutConverter_ConverterInjectedAutomatically()
        {
            var filter = EventFilter.ByType("com.example.order.placed");
            var opts   = new JsonSerializerOptions();          // no converter

            // Must not throw — converter is injected into an internal copy
            var ex = Record.Exception(() => filter.ToJson(opts));
            Assert.Null(ex);
        }

        [Fact]
        public static void ToJson_OptionsWithoutConverter_DoesNotMutateOriginalOptions()
        {
            var filter = EventFilter.ByType("com.example.order.placed");
            var opts   = new JsonSerializerOptions();

            _ = filter.ToJson(opts);

            // The caller's options must remain untouched
            Assert.Empty(opts.Converters);
        }

        [Fact]
        public static void ToJson_OptionsAlreadyHaveConverter_ConverterIsNotDuplicated()
        {
            var filter = EventFilter.ByType("com.example.order.placed");
            var opts   = new JsonSerializerOptions();
            opts.Converters.Add(new JsonFilterConverter());

            _ = filter.ToJson(opts);

            // No second instance should have been appended
            Assert.Single(opts.Converters);
        }

        [Fact]
        public static void ToJson_NullOptions_UsesDefaultOptions()
        {
            var filter = EventFilter.ByType("com.example.order.placed");

            var ex = Record.Exception(() => filter.ToJson(null));
            Assert.Null(ex);
        }

        // ── FromJson — guard clauses ───────────────────────────────────────────────────

        [Fact]
        public static void FromJson_NullInput_ThrowsArgumentException()
        {
            Assert.ThrowsAny<ArgumentException>(() => EventFilter.FromJson(null!));
        }

        [Fact]
        public static void FromJson_EmptyInput_ThrowsArgumentException()
        {
            Assert.ThrowsAny<ArgumentException>(() => EventFilter.FromJson(string.Empty));
        }

        [Fact]
        public static void FromJson_WhitespaceInput_ThrowsArgumentException()
        {
            Assert.ThrowsAny<ArgumentException>(() => EventFilter.FromJson("   "));
        }

        [Fact]
        public static void FromJson_InvalidJson_ThrowsJsonException()
        {
            Assert.Throws<JsonException>(() => EventFilter.FromJson("not-valid-json"));
        }

        [Fact]
        public static void FromJson_JsonNullLiteral_ReturnsNull()
        {
            var result = EventFilter.FromJson("null");
            Assert.Null(result);
        }

        // ── FromJson — custom JsonSerializerOptions ────────────────────────────────────

        [Fact]
        public static void FromJson_OptionsWithoutConverter_ConverterInjectedAutomatically()
        {
            var json = EventFilter.ByType("com.example.order.placed").ToJson();
            var opts = new JsonSerializerOptions();            // no converter

            var ex = Record.Exception(() => EventFilter.FromJson(json, opts));
            Assert.Null(ex);
        }

        [Fact]
        public static void FromJson_OptionsWithoutConverter_DoesNotMutateOriginalOptions()
        {
            var json = EventFilter.ByType("com.example.order.placed").ToJson();
            var opts = new JsonSerializerOptions();

            _ = EventFilter.FromJson(json, opts);

            Assert.Empty(opts.Converters);
        }

        [Fact]
        public static void FromJson_OptionsAlreadyHaveConverter_ConverterIsNotDuplicated()
        {
            var json = EventFilter.ByType("com.example.order.placed").ToJson();
            var opts = new JsonSerializerOptions();
            opts.Converters.Add(new JsonFilterConverter());

            _ = EventFilter.FromJson(json, opts);

            Assert.Single(opts.Converters);
        }

        [Fact]
        public static void FromJson_NullOptions_UsesDefaultOptions()
        {
            var json   = EventFilter.ByType("com.example.order.placed").ToJson();
            var result = EventFilter.FromJson(json, null);

            Assert.NotNull(result);
        }

        // ── Round-trip — structural correctness (functional matching) ──────────────────

        [Fact]
        public static void RoundTrip_ByType_MatchesSameEvents()
        {
            var original  = EventFilter.ByType("com.example.order.placed");
            var json      = original.ToJson();
            var restored  = EventFilter.FromJson(json)!;

            var matchingEvent    = MakeEvent("com.example.order.placed");
            var nonMatchingEvent = MakeEvent("com.example.order.updated");

            Assert.True(Matches(restored, matchingEvent));
            Assert.False(Matches(restored, nonMatchingEvent));
        }

        [Fact]
        public static void RoundTrip_ByTypePattern_MatchesSameEvents()
        {
            var original = EventFilter.ByTypePattern("com.example.*");
            var restored = EventFilter.FromJson(original.ToJson())!;

            Assert.True(Matches(restored,  MakeEvent("com.example.order.placed")));
            Assert.False(Matches(restored, MakeEvent("com.other.event")));
        }

        [Fact]
        public static void RoundTrip_BySource_MatchesSameEvents()
        {
            // Use a URL with an explicit path so the CLR Uri class does not normalise it
            // by appending a trailing slash (e.g. "https://api.example.com" → "https://api.example.com/").
            var original = EventFilter.BySource("https://api.example.com/orders");
            var restored = EventFilter.FromJson(original.ToJson())!;

            Assert.True(Matches(restored,  MakeEvent(source: "https://api.example.com/orders")));
            Assert.False(Matches(restored, MakeEvent(source: "https://other.example.com/orders")));
        }

        [Fact]
        public static void RoundTrip_BySubject_MatchesSameEvents()
        {
            var original = EventFilter.BySubject("order/42");
            var restored = EventFilter.FromJson(original.ToJson())!;

            Assert.True(Matches(restored,  MakeEvent(subject: "order/42")));
            Assert.False(Matches(restored, MakeEvent(subject: "order/99")));
        }

        [Fact]
        public static void RoundTrip_ByExtension_MatchesSameEvents()
        {
            var original = EventFilter.ByExtension("tenantid", "acme");
            var restored = EventFilter.FromJson(original.ToJson())!;

            Assert.True(Matches(restored,
                MakeEvent(extension: "tenantid", extensionValue: "acme")));
            Assert.False(Matches(restored,
                MakeEvent(extension: "tenantid", extensionValue: "globex")));
        }

        [Fact]
        public static void RoundTrip_ByField_StringValue_MatchesSameEvents()
        {
            var original = EventFilter.ByField("customer.tier", "gold");
            var restored = EventFilter.FromJson(original.ToJson())!;

            Assert.True(Matches(restored,  MakeEvent(data: new { customer = new { tier = "gold" } })));
            Assert.False(Matches(restored, MakeEvent(data: new { customer = new { tier = "silver" } })));
        }

        [Fact]
        public static void RoundTrip_ByField_NumericGreaterThan_MatchesSameEvents()
        {
            var original = EventFilter.ByField("payment.amount", FilterExpressionType.GreaterThan, 100.0);
            var restored = EventFilter.FromJson(original.ToJson())!;

            Assert.True(Matches(restored,  MakeEvent(data: new { payment = new { amount = 200 } })));
            Assert.False(Matches(restored, MakeEvent(data: new { payment = new { amount = 50 } })));
        }

        [Fact]
        public static void RoundTrip_ByField_BoolValue_MatchesSameEvents()
        {
            var original = EventFilter.ByField("payment.isPaid", FilterExpressionType.Equal, true);
            var restored = EventFilter.FromJson(original.ToJson())!;

            Assert.True(Matches(restored,  MakeEvent(data: new { payment = new { isPaid = true } })));
            Assert.False(Matches(restored, MakeEvent(data: new { payment = new { isPaid = false } })));
        }

        [Fact]
        public static void RoundTrip_FieldStartsWith_MatchesSameEvents()
        {
            var original = EventFilter.FieldStartsWith("customerId", "cust-");
            var restored = EventFilter.FromJson(original.ToJson())!;

            Assert.True(Matches(restored,  MakeEvent(data: new { customerId = "cust-42" })));
            Assert.False(Matches(restored, MakeEvent(data: new { customerId = "user-42" })));
        }

        [Fact]
        public static void RoundTrip_FieldEndsWith_MatchesSameEvents()
        {
            var original = EventFilter.FieldEndsWith("email", "@example.com");
            var restored = EventFilter.FromJson(original.ToJson())!;

            Assert.True(Matches(restored,  MakeEvent(data: new { email = "alice@example.com" })));
            Assert.False(Matches(restored, MakeEvent(data: new { email = "alice@other.com" })));
        }

        [Fact]
        public static void RoundTrip_FieldContains_MatchesSameEvents()
        {
            var original = EventFilter.FieldContains("notes", "urgent");
            var restored = EventFilter.FromJson(original.ToJson())!;

            Assert.True(Matches(restored,  MakeEvent(data: new { notes = "this is urgent" })));
            Assert.False(Matches(restored, MakeEvent(data: new { notes = "routine" })));
        }

        [Fact]
        public static void RoundTrip_FieldExists_MatchesSameEvents()
        {
            var original = EventFilter.FieldExists("loyaltyCard");
            var restored = EventFilter.FromJson(original.ToJson())!;

            Assert.True(Matches(restored,  MakeEvent(data: new { loyaltyCard = "LC-001" })));
            Assert.False(Matches(restored, MakeEvent(data: new { orderId = "ord-1" })));
        }

        [Fact]
        public static void RoundTrip_FieldNotExists_MatchesSameEvents()
        {
            var original = EventFilter.FieldNotExists("deletedAt");
            var restored = EventFilter.FromJson(original.ToJson())!;

            Assert.True(Matches(restored,  MakeEvent(data: new { status = "active" })));
            Assert.False(Matches(restored, MakeEvent(data: new { deletedAt = "2024-01-01" })));
        }

        [Fact]
        public static void RoundTrip_CompoundAllFilter_MatchesSameEvents()
        {
            var original = EventFilter.All(
                EventFilter.ByTypePattern("com.example.order.*"),
                EventFilter.ByField("customer.tier", "gold"));

            var restored = EventFilter.FromJson(original.ToJson())!;

            // Both conditions match
            Assert.True(Matches(restored,
                MakeEvent("com.example.order.placed",
                    data: new { customer = new { tier = "gold" } })));

            // Wrong type
            Assert.False(Matches(restored,
                MakeEvent("com.other.event",
                    data: new { customer = new { tier = "gold" } })));

            // Wrong tier
            Assert.False(Matches(restored,
                MakeEvent("com.example.order.placed",
                    data: new { customer = new { tier = "silver" } })));
        }

        [Fact]
        public static void RoundTrip_CompoundAnyFilter_MatchesSameEvents()
        {
            var original = EventFilter.Any(
                EventFilter.ByField("status", "placed"),
                EventFilter.ByField("status", "confirmed"));

            var restored = EventFilter.FromJson(original.ToJson())!;

            Assert.True(Matches(restored,  MakeEvent(data: new { status = "placed" })));
            Assert.True(Matches(restored,  MakeEvent(data: new { status = "confirmed" })));
            Assert.False(Matches(restored, MakeEvent(data: new { status = "cancelled" })));
        }

        [Fact]
        public static void RoundTrip_NestedFilter_MatchesSameEvents()
        {
            // (tier == "gold" AND amount > 100) OR priority == "urgent"
            var original = EventFilter.Any(
                EventFilter.All(
                    EventFilter.ByField("customer.tier", "gold"),
                    EventFilter.ByField("payment.amount", FilterExpressionType.GreaterThan, 100.0)),
                EventFilter.ByField("priority", "urgent"));

            var restored = EventFilter.FromJson(original.ToJson())!;

            // Matches via the AND branch
            Assert.True(Matches(restored, MakeEvent(data: new
            {
                customer = new { tier = "gold" },
                payment  = new { amount = 200 },
                priority = "normal"
            })));

            // Matches via the OR branch (urgent priority, regardless of tier/amount)
            Assert.True(Matches(restored, MakeEvent(data: new
            {
                customer = new { tier = "silver" },
                payment  = new { amount = 50 },
                priority = "urgent"
            })));

            // Neither branch matches
            Assert.False(Matches(restored, MakeEvent(data: new
            {
                customer = new { tier = "silver" },
                payment  = new { amount = 50 },
                priority = "normal"
            })));
        }

        [Fact]
        public static void RoundTrip_FluentBuilderFilter_MatchesSameEvents()
        {
            var original = EventFilter.New()
                .AnyOf(b => b
                    .ByType("com.example.order.placed")
                    .ByType("com.example.order.updated"))
                .Not(b => b.WithField("status", "cancelled"))
                .Build();

            var restored = EventFilter.FromJson(original.ToJson())!;

            Assert.True(Matches(restored,
                MakeEvent("com.example.order.placed", data: new { status = "active" })));
            Assert.True(Matches(restored,
                MakeEvent("com.example.order.updated", data: new { status = "active" })));

            // Excluded by the Not (cancelled status)
            Assert.False(Matches(restored,
                MakeEvent("com.example.order.placed", data: new { status = "cancelled" })));

            // Excluded by the AnyOf (wrong type)
            Assert.False(Matches(restored,
                MakeEvent("com.example.order.deleted", data: new { status = "active" })));
        }

        // ── Round-trip with custom options ─────────────────────────────────────────────

        [Fact]
        public static void RoundTrip_WithCustomOptions_ConverterAutoInjected_Produces_MatchingFilter()
        {
            var original = EventFilter.ByField("customer.tier", "gold");

            var opts = new JsonSerializerOptions { WriteIndented = true };

            // Serialize with custom options (converter auto-injected into a copy)
            var json = original.ToJson(opts);

            // Deserialize with different custom options (also without converter)
            var deserializeOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var restored = EventFilter.FromJson(json, deserializeOpts)!;

            Assert.True(Matches(restored,  MakeEvent(data: new { customer = new { tier = "gold" } })));
            Assert.False(Matches(restored, MakeEvent(data: new { customer = new { tier = "silver" } })));

            // Neither opts instance should have been mutated
            Assert.Empty(opts.Converters);
            Assert.Empty(deserializeOpts.Converters);
        }

        // ── Property-level [JsonConverter] annotation ──────────────────────────────────

        /// <summary>
        /// A DTO that uses the <c>[JsonConverter]</c> attribute on its
        /// <see cref="FilterExpression"/> property.
        /// </summary>
        private sealed class SubscriptionDto
        {
            public string Name { get; set; } = string.Empty;

            [JsonConverter(typeof(JsonFilterConverter))]
            public FilterExpression? Filter { get; set; }
        }

        /// <summary>
        /// A DTO that does NOT use the <c>[JsonConverter]</c> attribute; global registration
        /// via <see cref="JsonSerializerOptions"/> is required for both directions.
        /// </summary>
        private sealed class SubscriptionDtoPlain
        {
            public string Name { get; set; } = string.Empty;
            public FilterExpression? Filter { get; set; }
        }

        [Fact]
        public static void PropertyAnnotation_DtoWithJsonConverterAttribute_CanSerialize()
        {
            // [JsonConverter] on a property is sufficient for *serialization*:
            // the converter is invoked for the property value and produces valid JSON.
            var dto = new SubscriptionDto
            {
                Name   = "gold-orders",
                Filter = EventFilter.ByField("customer.tier", "gold")
            };

            var ex = Record.Exception(() => JsonSerializer.Serialize(dto));
            Assert.Null(ex);
        }

        [Fact]
        public static void PropertyAnnotation_DtoWithJsonConverterAttribute_CannotDeserialize()
        {
            // [JsonConverter] on a property is NOT sufficient for *deserialization* of
            // FilterExpression: the converter calls back into the serializer internally to
            // dispatch sub-expression types, and those calls use the property-scoped options
            // which do NOT include JsonFilterConverter for nested abstract types.
            // This results in a NotSupportedException (abstract type cannot be deserialized).
            var dto  = new SubscriptionDto { Name = "x", Filter = EventFilter.ByField("status", "active") };
            var json = JsonSerializer.Serialize(dto);

            Assert.ThrowsAny<Exception>(() => JsonSerializer.Deserialize<SubscriptionDto>(json));
        }

        [Fact]
        public static void Dto_WithGloballyRegisteredConverter_RoundTrips()
        {
            // The correct approach for DTOs is to register JsonFilterConverter globally
            // in JsonSerializerOptions; this covers both serialization and deserialization
            // of all FilterExpression properties without any property-level attribute.
            var globalOpts = new JsonSerializerOptions();
            globalOpts.Converters.Add(new JsonFilterConverter());

            var dto = new SubscriptionDtoPlain
            {
                Name   = "gold-orders",
                Filter = EventFilter.All(
                    EventFilter.ByTypePattern("com.example.order.*"),
                    EventFilter.ByField("customer.tier", "gold"))
            };

            var json     = JsonSerializer.Serialize(dto, globalOpts);
            var restored = JsonSerializer.Deserialize<SubscriptionDtoPlain>(json, globalOpts)!;

            Assert.NotNull(restored.Filter);
            Assert.Equal("gold-orders", restored.Name);

            Assert.True(Matches(restored.Filter,
                MakeEvent("com.example.order.placed",
                    data: new { customer = new { tier = "gold" } })));
            Assert.False(Matches(restored.Filter,
                MakeEvent("com.example.order.placed",
                    data: new { customer = new { tier = "silver" } })));
        }

        [Fact]
        public static void Dto_WithGloballyRegisteredConverter_NullFilterProperty_RoundTrips()
        {
            var globalOpts = new JsonSerializerOptions();
            globalOpts.Converters.Add(new JsonFilterConverter());

            var dto      = new SubscriptionDtoPlain { Name = "empty", Filter = null };
            var json     = JsonSerializer.Serialize(dto, globalOpts);
            var restored = JsonSerializer.Deserialize<SubscriptionDtoPlain>(json, globalOpts)!;

            Assert.Equal("empty", restored.Name);
            Assert.Null(restored.Filter);
        }
    }
}
