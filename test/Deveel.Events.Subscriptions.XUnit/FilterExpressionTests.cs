//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Text.Json;

using CloudNative.CloudEvents;

using Microsoft.Extensions.DependencyInjection;

namespace Deveel.Events
{
    [Trait("Feature", "Subscriptions")]
    [Trait("Feature", "SerializableFilter")]
    public static class FilterExpressionTests
    {
        // ── helpers ────────────────────────────────────────────────────────────────

        private static JsonElement Parse(string json)
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }

        // ── JsonPathComparisonExpression ───────────────────────────────────────────

        [Fact]
        public static void Equals_TopLevel_Matches()
        {
            var expr = FilterExpression.JsonPath("status", FilterOperator.Equals, "active");
            Assert.True(expr.Evaluate(Parse("""{"status":"active"}""")));
        }

        [Fact]
        public static void Equals_TopLevel_NoMatch()
        {
            var expr = FilterExpression.JsonPath("status", "active");
            Assert.False(expr.Evaluate(Parse("""{"status":"inactive"}""")));
        }

        [Fact]
        public static void NotEquals_Matches()
        {
            var expr = FilterExpression.JsonPath("status", FilterOperator.NotEquals, "deleted");
            Assert.True(expr.Evaluate(Parse("""{"status":"active"}""")));
        }

        [Fact]
        public static void StartsWith_Matches()
        {
            var expr = FilterExpression.JsonPath("type", FilterOperator.StartsWith, "order.");
            Assert.True(expr.Evaluate(Parse("""{"type":"order.placed"}""")));
            Assert.False(expr.Evaluate(Parse("""{"type":"payment.confirmed"}""")));
        }

        [Fact]
        public static void EndsWith_Matches()
        {
            var expr = FilterExpression.JsonPath("type", FilterOperator.EndsWith, ".placed");
            Assert.True(expr.Evaluate(Parse("""{"type":"order.placed"}""")));
            Assert.False(expr.Evaluate(Parse("""{"type":"order.updated"}""")));
        }

        [Fact]
        public static void Contains_Matches()
        {
            var expr = FilterExpression.JsonPath("note", FilterOperator.Contains, "urgent");
            Assert.True(expr.Evaluate(Parse("""{"note":"This is an urgent request"}""")));
            Assert.False(expr.Evaluate(Parse("""{"note":"Normal request"}""")));
        }

        [Fact]
        public static void GreaterThan_Matches()
        {
            var expr = FilterExpression.JsonPath("amount", FilterOperator.GreaterThan, "100");
            Assert.True(expr.Evaluate(Parse("""{"amount":150}""")));
            Assert.False(expr.Evaluate(Parse("""{"amount":50}""")));
        }

        [Fact]
        public static void LessThan_Matches()
        {
            var expr = FilterExpression.JsonPath("amount", FilterOperator.LessThan, "100");
            Assert.True(expr.Evaluate(Parse("""{"amount":50}""")));
            Assert.False(expr.Evaluate(Parse("""{"amount":200}""")));
        }

        [Fact]
        public static void GreaterThanOrEqual_Boundary()
        {
            var expr = FilterExpression.JsonPath("score", FilterOperator.GreaterThanOrEqual, "100");
            Assert.True(expr.Evaluate(Parse("""{"score":100}""")));
            Assert.True(expr.Evaluate(Parse("""{"score":101}""")));
            Assert.False(expr.Evaluate(Parse("""{"score":99}""")));
        }

        [Fact]
        public static void LessThanOrEqual_Boundary()
        {
            var expr = FilterExpression.JsonPath("score", FilterOperator.LessThanOrEqual, "100");
            Assert.True(expr.Evaluate(Parse("""{"score":100}""")));
            Assert.True(expr.Evaluate(Parse("""{"score":99}""")));
            Assert.False(expr.Evaluate(Parse("""{"score":101}""")));
        }

        [Fact]
        public static void Exists_PresentProperty()
        {
            var expr = FilterExpression.JsonPath("metadata", FilterOperator.Exists);
            Assert.True(expr.Evaluate(Parse("""{"metadata":{"tag":"foo"}}""")));
        }

        [Fact]
        public static void Exists_AbsentProperty()
        {
            var expr = FilterExpression.JsonPath("metadata", FilterOperator.Exists);
            Assert.False(expr.Evaluate(Parse("""{"status":"ok"}""")));
        }

        [Fact]
        public static void NotExists_AbsentProperty()
        {
            var expr = FilterExpression.JsonPath("deletedAt", FilterOperator.NotExists);
            Assert.True(expr.Evaluate(Parse("""{"status":"ok"}""")));
        }

        [Fact]
        public static void NotExists_PresentProperty()
        {
            var expr = FilterExpression.JsonPath("deletedAt", FilterOperator.NotExists);
            Assert.False(expr.Evaluate(Parse("""{"deletedAt":"2026-01-01"}""")));
        }

        [Fact]
        public static void NestedPath_Matches()
        {
            var expr = FilterExpression.JsonPath("order.customer.tier", "gold");
            Assert.True(expr.Evaluate(Parse("""{"order":{"customer":{"tier":"gold"}}}""")));
        }

        [Fact]
        public static void NestedPath_MissingSegment_ReturnsFalse()
        {
            var expr = FilterExpression.JsonPath("order.customer.tier", "gold");
            Assert.False(expr.Evaluate(Parse("""{"order":{}}""")));
        }

        // ── Logical combinators ────────────────────────────────────────────────────

        [Fact]
        public static void And_AllPass_ReturnsTrue()
        {
            var expr = FilterExpression.And(
                FilterExpression.JsonPath("tier", "gold"),
                FilterExpression.JsonPath("amount", FilterOperator.GreaterThan, "100"));

            Assert.True(expr.Evaluate(Parse("""{"tier":"gold","amount":250}""")));
        }

        [Fact]
        public static void And_OneFails_ReturnsFalse()
        {
            var expr = FilterExpression.And(
                FilterExpression.JsonPath("tier", "gold"),
                FilterExpression.JsonPath("amount", FilterOperator.GreaterThan, "100"));

            Assert.False(expr.Evaluate(Parse("""{"tier":"gold","amount":50}""")));
        }

        [Fact]
        public static void And_EmptyOperands_ReturnsTrue()
        {
            Assert.True(new AndFilterExpression().Evaluate(Parse("""{}""")));
        }

        [Fact]
        public static void Or_OneMatches_ReturnsTrue()
        {
            var expr = FilterExpression.Or(
                FilterExpression.JsonPath("tier", "gold"),
                FilterExpression.JsonPath("tier", "platinum"));

            Assert.True(expr.Evaluate(Parse("""{"tier":"platinum"}""")));
        }

        [Fact]
        public static void Or_NoneMach_ReturnsFalse()
        {
            var expr = FilterExpression.Or(
                FilterExpression.JsonPath("tier", "gold"),
                FilterExpression.JsonPath("tier", "platinum"));

            Assert.False(expr.Evaluate(Parse("""{"tier":"silver"}""")));
        }

        [Fact]
        public static void Or_EmptyOperands_ReturnsFalse()
        {
            Assert.False(new OrFilterExpression().Evaluate(Parse("""{}""")));
        }

        [Fact]
        public static void Not_Negates()
        {
            var expr = FilterExpression.Not(
                FilterExpression.JsonPath("status", "deleted"));

            Assert.True(expr.Evaluate(Parse("""{"status":"active"}""")));
            Assert.False(expr.Evaluate(Parse("""{"status":"deleted"}""")));
        }

        [Fact]
        public static void NestedLogical_ComplexExpression()
        {
            // (tier == "gold" OR amount > 1000) AND NOT status == "suspended"
            var expr = FilterExpression.And(
                FilterExpression.Or(
                    FilterExpression.JsonPath("tier", "gold"),
                    FilterExpression.JsonPath("amount", FilterOperator.GreaterThan, "1000")),
                FilterExpression.Not(
                    FilterExpression.JsonPath("status", "suspended")));

            Assert.True(expr.Evaluate(Parse("""{"tier":"gold","amount":50,"status":"active"}""")));
            Assert.True(expr.Evaluate(Parse("""{"tier":"silver","amount":2000,"status":"active"}""")));
            Assert.False(expr.Evaluate(Parse("""{"tier":"gold","amount":50,"status":"suspended"}""")));
            Assert.False(expr.Evaluate(Parse("""{"tier":"silver","amount":50,"status":"active"}""")));
        }
    }

    // ────────────────────────────────────────────────────────────────────────────────

    [Trait("Feature", "Subscriptions")]
    [Trait("Feature", "SerializableFilter")]
    public static class FilterExpressionJsonTests
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        private static T? RoundTrip<T>(T value)
        {
            var json = JsonSerializer.Serialize(value, Options);
            return JsonSerializer.Deserialize<T>(json, Options);
        }

        [Fact]
        public static void JsonPathComparison_RoundTrips()
        {
            var original = FilterExpression.JsonPath("order.tier", FilterOperator.Equals, "gold");
            var restored = RoundTrip<FilterExpression>(original) as JsonPathComparisonExpression;

            Assert.NotNull(restored);
            Assert.Equal("order.tier", restored.Path);
            Assert.Equal(FilterOperator.Equals, restored.Operator);
            Assert.Equal("gold", restored.Value);
        }

        [Fact]
        public static void AndExpression_RoundTrips()
        {
            var original = FilterExpression.And(
                FilterExpression.JsonPath("tier", "gold"),
                FilterExpression.JsonPath("amount", FilterOperator.GreaterThan, "100"));

            var restored = RoundTrip<FilterExpression>(original) as AndFilterExpression;
            Assert.NotNull(restored);
            Assert.Equal(2, restored.Operands.Count);
        }

        [Fact]
        public static void NotExpression_RoundTrips()
        {
            var original = FilterExpression.Not(FilterExpression.JsonPath("status", "deleted"));
            var restored = RoundTrip<FilterExpression>(original) as NotFilterExpression;
            Assert.NotNull(restored);
            Assert.IsType<JsonPathComparisonExpression>(restored.Operand);
        }

        [Fact]
        public static void NestedLogical_PreservesEvaluationAfterRoundTrip()
        {
            var original = FilterExpression.And(
                FilterExpression.JsonPath("tier", "gold"),
                FilterExpression.JsonPath("amount", FilterOperator.GreaterThan, "100"));

            var restored = RoundTrip<FilterExpression>(original)!;

            using var doc = JsonDocument.Parse("""{"tier":"gold","amount":250}""");
            Assert.True(restored.Evaluate(doc.RootElement));

            using var doc2 = JsonDocument.Parse("""{"tier":"gold","amount":50}""");
            Assert.False(restored.Evaluate(doc2.RootElement));
        }

        [Fact]
        public static void JsonContainsKindDiscriminator()
        {
            var expr = FilterExpression.JsonPath("tier", "gold");
            var json = JsonSerializer.Serialize<FilterExpression>(expr, Options);
            Assert.Contains("$kind", json);
            Assert.Contains("jsonPath", json);
        }
    }

    // ────────────────────────────────────────────────────────────────────────────────

    [Trait("Feature", "Subscriptions")]
    [Trait("Feature", "SerializableFilter")]
    public static class EventSubscriptionFilterModelTests
    {
        // ── AttributeFilterModel ───────────────────────────────────────────────────

        [Fact]
        public static void AttributeFilterModel_ExactRoundTrips()
        {
            var model = AttributeFilterModel.Exact("com.example.order.placed");
            var runtime = model.ToAttributeFilter();

            Assert.Equal(FilterMatchMode.Exact, runtime.MatchMode);
            Assert.Equal("com.example.order.placed", runtime.Value);
        }

        [Fact]
        public static void AttributeFilterModel_PrefixRoundTrips()
        {
            var model = AttributeFilterModel.Prefix("com.example.*");
            var runtime = model.ToAttributeFilter();

            Assert.Equal(FilterMatchMode.Prefix, runtime.MatchMode);
            Assert.True(runtime.Matches("com.example.order.placed"));
        }

        [Fact]
        public static void AttributeFilterModel_From_RuntimeFilter()
        {
            var runtime = EventAttributeFilter.Suffix("*.placed");
            var model = AttributeFilterModel.From(runtime);

            Assert.Equal(FilterMatchMode.Suffix, model.MatchMode);
            Assert.Equal(".placed", model.Value);
        }

        // ── EventSubscriptionFilterModel ──────────────────────────────────────────

        [Fact]
        public static void FilterModel_ToRuntimeFilter_EnvelopeFilters()
        {
            var model = new EventSubscriptionFilterModel
            {
                Type   = AttributeFilterModel.Prefix("com.example.*"),
                Source = AttributeFilterModel.Exact("https://example.com/"),
            };

            var runtime = model.ToRuntimeFilter();

            Assert.NotNull(runtime.TypeFilter);
            Assert.True(runtime.TypeFilter.Matches("com.example.order.placed"));
            Assert.NotNull(runtime.SourceFilter);
            Assert.True(runtime.SourceFilter.Matches("https://example.com/"));
        }

        [Fact]
        public static void FilterModel_WithDataExpression_ToRuntimeFilter()
        {
            var model = new EventSubscriptionFilterModel
            {
                Type = AttributeFilterModel.Exact("com.example.order"),
                DataExpression = FilterExpression.JsonPath("tier", "gold")
            };

            var runtime = model.ToRuntimeFilter();
            Assert.NotNull(runtime.DataFilter);

            var e = new CloudEvent
            {
                Type = "com.example.order",
                Source = new Uri("https://example.com"),
                Id = Guid.NewGuid().ToString("N"),
                DataContentType = "application/json",
                Data = new { tier = "gold" }
            };

            Assert.True(runtime.DataFilter.Matches(e));
        }

        [Fact]
        public static void FilterModel_MatchesCloudEvent_EndToEnd()
        {
            var model = new EventSubscriptionFilterModel
            {
                Type = AttributeFilterModel.Prefix("com.example.*"),
                DataExpression = FilterExpression.And(
                    FilterExpression.JsonPath("tier", "gold"),
                    FilterExpression.JsonPath("amount", FilterOperator.GreaterThan, "100"))
            };

            var runtime = model.ToRuntimeFilter();

            var matching = new CloudEvent
            {
                Type = "com.example.order.placed",
                Source = new Uri("https://example.com"),
                Id = Guid.NewGuid().ToString("N"),
                DataContentType = "application/json",
                Data = new { tier = "gold", amount = 250 }
            };

            var notMatching = new CloudEvent
            {
                Type = "com.example.order.placed",
                Source = new Uri("https://example.com"),
                Id = Guid.NewGuid().ToString("N"),
                DataContentType = "application/json",
                Data = new { tier = "silver", amount = 250 }
            };

            Assert.True(runtime.Matches(matching));
            Assert.False(runtime.Matches(notMatching));
        }

        // ── From(EventSubscriptionFilter) ─────────────────────────────────────────

        [Fact]
        public static void From_RuntimeFilter_CapturesEnvelopeFilters()
        {
            var filter = EventSubscriptionFilter.Builder
                .WithTypePattern("com.example.*")
                .WithSource("https://example.com/")
                .Build();

            var model = EventSubscriptionFilterModel.From(filter);

            Assert.NotNull(model.Type);
            Assert.Equal(FilterMatchMode.Prefix, model.Type.MatchMode);
            Assert.NotNull(model.Source);
            Assert.Equal(FilterMatchMode.Exact, model.Source.MatchMode);
            Assert.False(model.HasUnserializablePredicates);
        }

        [Fact]
        public static void From_RuntimeFilter_WithJsonPathDataFilter_CapturesExpression()
        {
            var filter = EventSubscriptionFilter.Builder
                .WithType("com.example.order")
                .WithJsonPath("tier", "gold")
                .Build();

            var model = EventSubscriptionFilterModel.From(filter);

            Assert.NotNull(model.DataExpression);
            Assert.IsType<JsonPathComparisonExpression>(model.DataExpression);
            Assert.False(model.HasUnserializablePredicates);
        }

        [Fact]
        public static void From_RuntimeFilter_WithJsonPredicate_SetsUnserializableFlag()
        {
            var filter = EventSubscriptionFilter.Builder
                .WithJsonPredicate(_ => true)
                .Build();

            var model = EventSubscriptionFilterModel.From(filter);

            Assert.Null(model.DataExpression);
            Assert.True(model.HasUnserializablePredicates);
        }

        [Fact]
        public static void From_RuntimeFilter_WithDelegatePredicate_SetsUnserializableFlag()
        {
            var filter = EventSubscriptionFilter.Builder
                .WithPredicate(_ => true)
                .Build();

            var model = EventSubscriptionFilterModel.From(filter);

            Assert.True(model.HasUnserializablePredicates);
        }

        // ── JSON serialization ────────────────────────────────────────────────────

        [Fact]
        public static void FilterModel_JsonRoundTrip_PreservesEverything()
        {
            var model = new EventSubscriptionFilterModel
            {
                Type   = AttributeFilterModel.Prefix("com.example.*"),
                Source = AttributeFilterModel.Exact("https://example.com/"),
                Extensions = new Dictionary<string, AttributeFilterModel>
                {
                    ["env"] = AttributeFilterModel.Exact("production")
                },
                DataExpression = FilterExpression.And(
                    FilterExpression.JsonPath("tier", "gold"),
                    FilterExpression.JsonPath("amount", FilterOperator.GreaterThan, "100"))
            };

            var json  = model.ToJson();
            var restored = EventSubscriptionFilterModel.FromJson(json)!;

            Assert.NotNull(restored);
            Assert.Equal(FilterMatchMode.Prefix, restored.Type!.MatchMode);
            Assert.Equal(FilterMatchMode.Exact,  restored.Source!.MatchMode);
            Assert.Single(restored.Extensions!);
            Assert.IsType<AndFilterExpression>(restored.DataExpression);

            // Restored model evaluates identically to the original.
            var runtime = restored.ToRuntimeFilter();
            var e = new CloudEvent
            {
                Type = "com.example.order.placed",
                Source = new Uri("https://example.com/"),
                Id = Guid.NewGuid().ToString("N"),
                DataContentType = "application/json",
                Data = new { tier = "gold", amount = 250 }
            };
            // extension attribute
            var envAttr = CloudEventAttribute.CreateExtension("env", CloudEventAttributeType.String);
            e[envAttr] = "production";

            Assert.True(runtime.Matches(e));
        }

        // ── Builder integration ───────────────────────────────────────────────────

        [Fact]
        public static void Builder_WithDataExpression_Works()
        {
            var filter = EventSubscriptionFilter.Builder
                .WithType("com.example.order")
                .WithDataExpression(
                    FilterExpression.Or(
                        FilterExpression.JsonPath("tier", "gold"),
                        FilterExpression.JsonPath("tier", "platinum")))
                .Build();

            Assert.NotNull(filter.DataFilter);

            var gold = new CloudEvent
            {
                Type = "com.example.order",
                Source = new Uri("https://example.com"),
                Id = Guid.NewGuid().ToString("N"),
                DataContentType = "application/json",
                Data = new { tier = "gold" }
            };
            Assert.True(filter.DataFilter.Matches(gold));

            var silver = new CloudEvent
            {
                Type = "com.example.order",
                Source = new Uri("https://example.com"),
                Id = Guid.NewGuid().ToString("N"),
                DataContentType = "application/json",
                Data = new { tier = "silver" }
            };
            Assert.False(filter.DataFilter.Matches(silver));
        }

        // ── Dispatcher integration ────────────────────────────────────────────────

        [Fact]
        public static async Task FilterModel_RoutesViaDispatcher_WhenExpressionMatches()
        {
            var invoked = new List<string>();

            // Build subscription from a persisted model (e.g. loaded from DB).
            var model = new EventSubscriptionFilterModel
            {
                Type = AttributeFilterModel.Prefix("com.example.*"),
                DataExpression = FilterExpression.JsonPath("tier", "gold")
            };

            var runtimeFilter = model.ToRuntimeFilter();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventPublisher(opt => opt.Source = new Uri("https://example.com"))
                .AddDispatcher()
                .Subscribe(runtimeFilter,
                    (e, _) => { invoked.Add(e.Type!); return Task.CompletedTask; });

            var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<IEventPublisher>();

            CloudEvent Evt(string tier) => new()
            {
                Type = "com.example.order.placed",
                Source = new Uri("https://example.com"),
                Id = Guid.NewGuid().ToString("N"),
                Time = DateTimeOffset.UtcNow,
                DataContentType = "application/json",
                Data = new { tier }
            };

            await publisher.PublishEventAsync(Evt("gold"));    // match
            await publisher.PublishEventAsync(Evt("silver"));  // no match
            await publisher.PublishEventAsync(Evt("gold"));    // match

            Assert.Equal(2, invoked.Count);
        }
    }
}

