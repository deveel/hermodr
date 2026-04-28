//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

using Deveel.Filters;

namespace Deveel.Events
{
    /// <summary>
    /// Tests focused on registering new subscriptions into <see cref="EventSubscriptionRegistry"/>
    /// via both the synchronous <c>Register</c> and the asynchronous <c>RegisterAsync</c> paths,
    /// and verifying that registered subscriptions are correctly stored and retrievable.
    /// </summary>
    [Trait("Feature", "Subscriptions")]
    [Trait("Subject", "Registration")]
    public static class EventSubscriptionRegistryRegistrationTests
    {
        // ── helpers ────────────────────────────────────────────────────────────────────

        private static CloudEvent MakeEvent(
            string type = "com.example.order.placed",
            string source = "https://example.com")
            => new()
            {
                Type = type,
                Source = new Uri(source),
                Id = Guid.NewGuid().ToString("N")
            };

        private static EventSubscription MakeSub(
            FilterExpression? filter = null,
            string? name = null)
            => new(filter ?? FilterExpression.Constant(true),
                   (_, _) => Task.CompletedTask,
                   name);

        // ── synchronous Register ───────────────────────────────────────────────────────

        [Fact]
        public static void Register_NullSubscription_ThrowsArgumentNullException()
        {
            var registry = new EventSubscriptionRegistry();
            Assert.Throws<ArgumentNullException>(() => registry.Register(null!));
        }

        [Fact]
        public static void Register_SingleSubscription_IsRetrievable()
        {
            var registry = new EventSubscriptionRegistry();
            var sub = MakeSub(CloudEventFilter.ByType("com.example.order.placed"), "order-sub");

            registry.Register(sub);

            var matches = registry.GetMatchingSubscriptions(MakeEvent("com.example.order.placed"));
            Assert.Single(matches);
            Assert.Same(sub, matches[0]);
        }

        [Fact]
        public static void Register_SubscriptionWithName_NameIsPreserved()
        {
            var registry = new EventSubscriptionRegistry();
            var sub = MakeSub(name: "my-named-sub");

            registry.Register(sub);

            var matches = registry.GetMatchingSubscriptions(MakeEvent());
            Assert.Single(matches);
            Assert.Equal("my-named-sub", matches[0].Name);
        }

        [Fact]
        public static void Register_SubscriptionWithNoName_NameIsNull()
        {
            var registry = new EventSubscriptionRegistry();
            var sub = MakeSub();  // no name supplied

            registry.Register(sub);

            var matches = registry.GetMatchingSubscriptions(MakeEvent());
            Assert.Single(matches);
            Assert.Null(matches[0].Name);
        }

        [Fact]
        public static void Register_SameInstanceTwice_AppearsTwiceInMatches()
        {
            var registry = new EventSubscriptionRegistry();
            var sub = MakeSub();

            registry.Register(sub);
            registry.Register(sub);

            var matches = registry.GetMatchingSubscriptions(MakeEvent());
            Assert.Equal(2, matches.Count);
        }

        [Fact]
        public static async Task Register_ThenGetMatchingSubscriptionsAsync_ReturnsSameResult()
        {
            var registry = new EventSubscriptionRegistry();
            var sub = MakeSub(CloudEventFilter.ByType("com.example.order.placed"));

            registry.Register(sub);

            // Retrieve via async path after sync registration.
            var matches = await registry.ResolveSubscriptionsAsync(MakeEvent("com.example.order.placed"));

            Assert.Single(matches);
            Assert.Same(sub, matches[0]);
        }

        [Fact]
        public static void Register_AfterPreSeeding_AddsToExistingSubscriptions()
        {
            var seeded = MakeSub(CloudEventFilter.ByType("com.example.seeded"), "seeded");
            var registry = new EventSubscriptionRegistry([seeded]);

            var newSub = MakeSub(CloudEventFilter.ByType("com.example.new"), "new");
            registry.Register(newSub);

            Assert.Single(registry.GetMatchingSubscriptions(MakeEvent("com.example.seeded")));
            Assert.Single(registry.GetMatchingSubscriptions(MakeEvent("com.example.new")));
        }

        // ── asynchronous RegisterAsync ─────────────────────────────────────────────────

        [Fact]
        public static async Task RegisterAsync_NullSubscription_ThrowsArgumentNullException()
        {
            var registry = new EventSubscriptionRegistry();
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => registry.RegisterAsync(null!));
        }

        [Fact]
        public static async Task RegisterAsync_CancelledToken_ThrowsOperationCanceledException()
        {
            var registry = new EventSubscriptionRegistry();
            var sub = MakeSub();

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => registry.RegisterAsync(sub, cts.Token));
        }

        [Fact]
        public static async Task RegisterAsync_SingleSubscription_IsRetrievable()
        {
            var registry = new EventSubscriptionRegistry();
            var sub = MakeSub(CloudEventFilter.ByType("com.example.order.placed"), "async-sub");

            await registry.RegisterAsync(sub);

            var matches = await registry.ResolveSubscriptionsAsync(MakeEvent("com.example.order.placed"));
            Assert.Single(matches);
            Assert.Same(sub, matches[0]);
        }

        [Fact]
        public static async Task RegisterAsync_SubscriptionWithName_NameIsPreserved()
        {
            var registry = new EventSubscriptionRegistry();
            var sub = MakeSub(name: "named-async-sub");

            await registry.RegisterAsync(sub);

            var matches = await registry.ResolveSubscriptionsAsync(MakeEvent());
            Assert.Single(matches);
            Assert.Equal("named-async-sub", matches[0].Name);
        }

        [Fact]
        public static async Task RegisterAsync_EmptyFilter_MatchesAnyEvent()
        {
            var registry = new EventSubscriptionRegistry();
            var sub = MakeSub(FilterExpression.Constant(true));  // empty = match all

            await registry.RegisterAsync(sub);

            var matchesA = await registry.ResolveSubscriptionsAsync(MakeEvent("com.example.a"));
            var matchesB = await registry.ResolveSubscriptionsAsync(MakeEvent("com.example.b"));

            Assert.Single(matchesA);
            Assert.Single(matchesB);
        }

        [Fact]
        public static async Task RegisterAsync_SameInstanceTwice_AppearsTwiceInMatches()
        {
            var registry = new EventSubscriptionRegistry();
            var sub = MakeSub();

            await registry.RegisterAsync(sub);
            await registry.RegisterAsync(sub);

            var matches = await registry.ResolveSubscriptionsAsync(MakeEvent());
            Assert.Equal(2, matches.Count);
        }

        [Fact]
        public static async Task RegisterAsync_ThenGetMatchingSubscriptionsSync_ReturnsSameResult()
        {
            var registry = new EventSubscriptionRegistry();
            var sub = MakeSub(CloudEventFilter.ByType("com.example.order.placed"));

            await registry.RegisterAsync(sub);

            // Retrieve via sync path after async registration.
            var matches = registry.GetMatchingSubscriptions(MakeEvent("com.example.order.placed"));
            Assert.Single(matches);
            Assert.Same(sub, matches[0]);
        }

        [Fact]
        public static async Task RegisterAsync_DoesNotMatchFilteredOutEvent()
        {
            var registry = new EventSubscriptionRegistry();
            var sub = MakeSub(CloudEventFilter.ByType("com.example.specific"));

            await registry.RegisterAsync(sub);

            var matches = await registry.ResolveSubscriptionsAsync(MakeEvent("com.example.other"));
            Assert.Empty(matches);
        }

        [Fact]
        public static async Task RegisterAsync_AfterPreSeeding_AddsToExistingSubscriptions()
        {
            var seeded = MakeSub(CloudEventFilter.ByType("com.example.seeded"), "seeded");
            var registry = new EventSubscriptionRegistry([seeded]);

            var newSub = MakeSub(CloudEventFilter.ByType("com.example.new"), "new");
            await registry.RegisterAsync(newSub);

            Assert.Single(await registry.ResolveSubscriptionsAsync(MakeEvent("com.example.seeded")));
            Assert.Single(await registry.ResolveSubscriptionsAsync(MakeEvent("com.example.new")));
        }

        [Fact]
        public static async Task RegisterAsync_WithContext_IsRetrievableViaContextOverload()
        {
            var registry = new EventSubscriptionRegistry();
            var sub = MakeSub(CloudEventFilter.ByType("com.example.order.placed"));

            await registry.RegisterAsync(sub);

            // Use the context-aware overload (context=null means no DI services needed).
            var matches = await registry.ResolveSubscriptionsAsync(
                MakeEvent("com.example.order.placed"),
                context: null);

            Assert.Single(matches);
            Assert.Same(sub, matches[0]);
        }

        // ── mix of sync and async registrations ────────────────────────────────────────

        [Fact]
        public static async Task MixedRegistration_SyncAndAsync_BothSubscriptionsArePresent()
        {
            var registry = new EventSubscriptionRegistry();

            var syncSub = MakeSub(CloudEventFilter.ByType("com.example.sync"), "sync");
            var asyncSub = MakeSub(CloudEventFilter.ByType("com.example.async"), "async");

            registry.Register(syncSub);
            await registry.RegisterAsync(asyncSub);

            var matchSync = await registry.ResolveSubscriptionsAsync(MakeEvent("com.example.sync"));
            var matchAsync = await registry.ResolveSubscriptionsAsync(MakeEvent("com.example.async"));

            Assert.Single(matchSync);
            Assert.Same(syncSub, matchSync[0]);

            Assert.Single(matchAsync);
            Assert.Same(asyncSub, matchAsync[0]);
        }

        // ── thread safety ──────────────────────────────────────────────────────────────

        [Fact]
        public static void Register_ConcurrentCalls_AllSubscriptionsAreStored()
        {
            const int count = 100;
            var registry = new EventSubscriptionRegistry();
            var subs = Enumerable.Range(0, count)
                .Select(i => MakeSub(name: $"sub-{i}"))
                .ToList();

            Parallel.ForEach(subs, sub => registry.Register(sub));

            var matches = registry.GetMatchingSubscriptions(MakeEvent());
            Assert.Equal(count, matches.Count);
        }

        [Fact]
        public static async Task RegisterAsync_ConcurrentCalls_AllSubscriptionsAreStored()
        {
            const int count = 100;
            var registry = new EventSubscriptionRegistry();
            var subs = Enumerable.Range(0, count)
                .Select(i => MakeSub(name: $"async-sub-{i}"))
                .ToList();

            await Task.WhenAll(subs.Select(s => registry.RegisterAsync(s)));

            var matches = await registry.ResolveSubscriptionsAsync(MakeEvent());
            Assert.Equal(count, matches.Count);
        }
    }
}
