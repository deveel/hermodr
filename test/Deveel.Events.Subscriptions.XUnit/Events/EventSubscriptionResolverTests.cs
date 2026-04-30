//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

using Deveel.Filters;

using Microsoft.Extensions.DependencyInjection;

namespace Deveel.Events
{
    /// <summary>
    /// Tests that verify <see cref="IEventSubscriptionResolver"/> semantics: the read-only
        /// contract extracted from <see cref="IEventSubscriptionRegistry"/>, including
        /// read-only resolver implementations and multiple-resolver fan-out through the
        /// publisher middleware pipeline.
    /// </summary>
    [Trait("Feature", "Subscriptions")]
    [Trait("Subject", "Resolver")]
    public static class EventSubscriptionResolverTests
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

        // ── IEventSubscriptionRegistry is also an IEventSubscriptionResolver ──────────

        [Fact]
        public static void EventSubscriptionRegistry_ImplementsResolver()
        {
            var registry = new EventSubscriptionRegistry();
            Assert.IsAssignableFrom<IEventSubscriptionResolver>(registry);
        }

        [Fact]
        public static async Task Registry_AsResolver_ReturnsMatchingSubscriptions()
        {
            var sub = MakeSub(EventFilter.ByType("com.example.order.placed"), "order");
            var registry = new EventSubscriptionRegistry([sub]);

            IEventSubscriptionResolver resolver = registry;

            var matches = await resolver.ResolveSubscriptionsAsync(MakeEvent("com.example.order.placed"));
            Assert.Single(matches);
            Assert.Same(sub, matches[0]);
        }

        [Fact]
        public static async Task Registry_AsResolver_EmptyWhenNoMatch()
        {
            var sub = MakeSub(EventFilter.ByType("com.example.other"), "other");
            var registry = new EventSubscriptionRegistry([sub]);

            IEventSubscriptionResolver resolver = registry;

            var matches = await resolver.ResolveSubscriptionsAsync(MakeEvent("com.example.order.placed"));
            Assert.Empty(matches);
        }

        [Fact]
        public static async Task Registry_AsResolver_WithContextOverload_Works()
        {
            var sub = MakeSub(EventFilter.ByType("com.example.order.placed"), "order");
            var registry = new EventSubscriptionRegistry([sub]);

            IEventSubscriptionResolver resolver = registry;

            var matches = await resolver.ResolveSubscriptionsAsync(
                MakeEvent("com.example.order.placed"),
                context: null);

            Assert.Single(matches);
            Assert.Same(sub, matches[0]);
        }

        // ── read-only resolver (does not implement IEventSubscriptionRegistry) ─────────

        [Fact]
        public static async Task ReadOnlyResolver_CanBeUsedWithPublisherPipeline()
        {
            var handled = new List<string>();

            var sub = new EventSubscription(
                EventFilter.ByType("com.example.order.placed"),
                (e, _) => { handled.Add(e.Type!); return Task.CompletedTask; },
                "from-readonly");

            IEventSubscriptionResolver readonlyResolver = new StaticResolver([sub]);

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventPublisher(opt => opt.Source = new Uri("https://example.com"))
                .AddSubscriptions();
            services.AddSingleton(readonlyResolver);

            var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<EventPublisher>().UseDispatcher();

            await publisher.PublishEventAsync(MakeEvent("com.example.order.placed"));

            Assert.Single(handled);
            Assert.Equal("com.example.order.placed", handled[0]);
        }

        [Fact]
        public static async Task ReadOnlyResolver_DoesNotExposeRegisterAsync()
        {
            // StaticResolver only implements IEventSubscriptionResolver, NOT IEventSubscriptionRegistry.
            IEventSubscriptionResolver resolver = new StaticResolver([]);
            Assert.False(resolver is IEventSubscriptionRegistry);
            await Task.CompletedTask;
        }

        // ── multiple resolver fan-out via middleware ───────────────────────────────────

        [Fact]
        public static async Task MultipleResolvers_AllMatchingSubscriptionsAreDispatched()
        {
            var invoked = new List<string>();

            var sub1 = new EventSubscription(
                EventFilter.ByType("com.example.order.placed"),
                (_, _) => { invoked.Add("resolver-1"); return Task.CompletedTask; },
                "from-resolver-1");

            var sub2 = new EventSubscription(
                EventFilter.ByType("com.example.order.placed"),
                (_, _) => { invoked.Add("resolver-2"); return Task.CompletedTask; },
                "from-resolver-2");

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventPublisher(opt => opt.Source = new Uri("https://example.com"))
                .AddSubscriptions();
            services.AddSingleton<IEventSubscriptionResolver>(new StaticResolver([sub1]));
            services.AddSingleton<IEventSubscriptionResolver>(new StaticResolver([sub2]));

            var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<EventPublisher>().UseDispatcher();

            await publisher.PublishEventAsync(MakeEvent("com.example.order.placed"));

            Assert.Equal(["resolver-1", "resolver-2"], invoked);
        }

        [Fact]
        public static async Task MultipleResolvers_OnlyMatchingSubscriptionsFromEachAreDispatched()
        {
            var invoked = new List<string>();

            var orderSub = new EventSubscription(
                EventFilter.ByType("com.example.order.placed"),
                (_, _) => { invoked.Add("order"); return Task.CompletedTask; },
                "order");

            var userSub = new EventSubscription(
                EventFilter.ByType("com.example.user.created"),
                (_, _) => { invoked.Add("user"); return Task.CompletedTask; },
                "user");

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventPublisher(opt => opt.Source = new Uri("https://example.com"))
                .AddSubscriptions();
            services.AddSingleton<IEventSubscriptionResolver>(new StaticResolver([orderSub]));
            services.AddSingleton<IEventSubscriptionResolver>(new StaticResolver([userSub]));

            var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<EventPublisher>().UseDispatcher();

            // Only the order resolver should match.
            await publisher.PublishEventAsync(MakeEvent("com.example.order.placed"));

            Assert.Single(invoked);
            Assert.Equal("order", invoked[0]);
        }

        [Fact]
        public static async Task MultipleResolvers_NoMatchAcrossAllResolvers_NothingInvoked()
        {
            var invoked = false;

            var sub = new EventSubscription(
                EventFilter.ByType("com.example.other"),
                (_, _) => { invoked = true; return Task.CompletedTask; });

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventPublisher(opt => opt.Source = new Uri("https://example.com"))
                .AddSubscriptions();
            services.AddSingleton<IEventSubscriptionResolver>(new StaticResolver([sub]));
            services.AddSingleton<IEventSubscriptionResolver>(new StaticResolver([]));

            var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<EventPublisher>().UseDispatcher();

            await publisher.PublishEventAsync(MakeEvent("com.example.order.placed"));

            Assert.False(invoked);
        }

        [Fact]
        public static async Task MultipleResolvers_OrderIsPreserved()
        {
            var order = new List<string>();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventPublisher(opt => opt.Source = new Uri("https://example.com"))
                .AddSubscriptions();
            services.AddSingleton<IEventSubscriptionResolver>(new StaticResolver([
                new EventSubscription(FilterExpression.Constant(true),
                    (_, _) => { order.Add("A"); return Task.CompletedTask; }, "A")
            ]));
            services.AddSingleton<IEventSubscriptionResolver>(new StaticResolver([
                new EventSubscription(FilterExpression.Constant(true),
                    (_, _) => { order.Add("B"); return Task.CompletedTask; }, "B"),
                new EventSubscription(FilterExpression.Constant(true),
                    (_, _) => { order.Add("C"); return Task.CompletedTask; }, "C")
            ]));

            var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<EventPublisher>().UseDispatcher();

            await publisher.PublishEventAsync(MakeEvent());

            Assert.Equal(["A", "B", "C"], order);
        }

        // ── EventSubscriptionContext ───────────────────────────────────────────────────

        [Fact]
        public static void EventSubscriptionContext_Empty_HasNullServices()
        {
            Assert.Null(EventSubscriptionContext.Empty.Services);
        }

        [Fact]
        public static async Task Resolver_ReceivesContext_WithServicesFromEventContext()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<CapturingResolver>();
            services.AddEventPublisher(opt => opt.Source = new Uri("https://example.com"))
                .AddSubscriptions()
                .AddSubscriptionResolver<CapturingResolver>();

            var provider = services.BuildServiceProvider();
            var resolver = provider.GetRequiredService<CapturingResolver>();
            var publisher = provider.GetRequiredService<EventPublisher>()
                .UseDispatcher();

            await publisher.PublishEventAsync(MakeEvent());

            Assert.NotNull(resolver.ReceivedContext);
            // ResolvedInstance is set by CapturingResolver while the scope is alive;
            // we must not call Services.GetRequiredService<> here because the
            // per-publish DI scope has already been disposed.
            Assert.Same(resolver, resolver.ResolvedInstance);
        }

        [Fact]
        public static async Task Resolver_ReceivesContext_WhenDispatchedViaPublisher()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<CapturingResolver>();
            services.AddEventPublisher(opt => opt.Source = new Uri("https://example.com"))
                .AddSubscriptions()
                .AddSubscriptionResolver<CapturingResolver>();

            var provider = services.BuildServiceProvider();
            var resolver = provider.GetRequiredService<CapturingResolver>();
            var publisher = provider.GetRequiredService<EventPublisher>().UseDispatcher();

            await publisher.PublishEventAsync(MakeEvent());

            Assert.NotNull(resolver.ReceivedContext);
        }

        // ── DI: AddSubscriptionResolver<T> ────────────────────────────────────────────

        [Fact]
        public static async Task AddSubscriptionResolver_IsQueriedByDispatcher()
        {
            var invoked = new List<string>();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventPublisher(opt => opt.Source = new Uri("https://example.com"))
                .AddSubscriptions()
                // Inline subscription on the registry.
                .Subscribe("com.example.order.placed",
                    (_, _) => { invoked.Add("registry"); return Task.CompletedTask; },
                    name: "registry-sub")
                // Read-only custom resolver.
                .AddSubscriptionResolver<ReadOnlyOrderResolver>();

            var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<EventPublisher>()
                .UseDispatcher();

            // Wire the invoked list into the resolver instance.
            var resolver = provider.GetRequiredService<ReadOnlyOrderResolver>();
            resolver.Invoked = invoked;

            await publisher.PublishEventAsync(new CloudEvent
            {
                Type = "com.example.order.placed",
                Source = new Uri("https://example.com"),
                Id = Guid.NewGuid().ToString("N"),
                Time = DateTimeOffset.UtcNow
            });

            // Both the registry subscription and the custom resolver's subscription must fire.
            Assert.Equal(2, invoked.Count);
            Assert.Contains("registry", invoked);
            Assert.Contains("readonly-resolver", invoked);
        }

        [Fact]
        public static void AddSubscriptionResolver_IsRegisteredAsIEventSubscriptionResolver()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventPublisher()
                .AddSubscriptions()
                .AddSubscriptionResolver<StaticEmptyResolver>();

            var provider = services.BuildServiceProvider();

            var resolvers = provider.GetServices<IEventSubscriptionResolver>().ToList();
            // At least 2: the built-in registry + the custom one.
            Assert.True(resolvers.Count >= 2);
            Assert.Contains(resolvers, r => r is StaticEmptyResolver);
        }

        [Fact]
        public static void AddSubscriptions_RegistryIsAlsoExposedAsResolver()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventPublisher()
                .AddSubscriptions();

            var provider = services.BuildServiceProvider();

            var registry = provider.GetRequiredService<IEventSubscriptionRegistry>();
            var resolvers = provider.GetServices<IEventSubscriptionResolver>().ToList();

            // The registry instance must appear among the resolvers.
            Assert.Contains(registry, resolvers);
        }

        // ── helper types ───────────────────────────────────────────────────────────────

        /// <summary>
        /// A minimal read-only resolver backed by a fixed list of subscriptions.
        /// Deliberately does NOT implement <see cref="IEventSubscriptionRegistry"/>.
        /// </summary>
        private sealed class StaticResolver(IReadOnlyList<IEventSubscription> subscriptions)
            : IEventSubscriptionResolver
        {
            public Task<IReadOnlyList<IEventSubscription>> ResolveSubscriptionsAsync(
                CloudEvent @event, CancellationToken cancellationToken = default)
            {
                IReadOnlyList<IEventSubscription> result = subscriptions
                    .Where(s => s.Filter.Matches(@event, EventSubscriptionContext.Empty))
                    .ToList();
                return Task.FromResult(result);
            }
        }

        /// <summary>A read-only resolver that always returns an empty list.</summary>
        private sealed class StaticEmptyResolver : IEventSubscriptionResolver
        {
            public Task<IReadOnlyList<IEventSubscription>> ResolveSubscriptionsAsync(
                CloudEvent @event, CancellationToken cancellationToken = default)
                => Task.FromResult<IReadOnlyList<IEventSubscription>>([]);
        }

        /// <summary>
        /// A resolver that captures the <see cref="EventSubscriptionContext"/> it receives.
        /// Always returns an empty subscription list.
        /// </summary>
        private sealed class CapturingResolver : IEventSubscriptionResolver
        {
            public EventSubscriptionContext? ReceivedContext { get; private set; }

            /// <summary>
            /// Set during <see cref="ResolveSubscriptionsAsync(CloudEvent, EventSubscriptionContext?, CancellationToken)"/>
            /// while the DI scope is still alive, so assertions can verify the resolved instance
            /// without accessing the already-disposed scoped <see cref="IServiceProvider"/> afterwards.
            /// </summary>
            public CapturingResolver? ResolvedInstance { get; private set; }

            public Task<IReadOnlyList<IEventSubscription>> ResolveSubscriptionsAsync(
                CloudEvent @event, CancellationToken cancellationToken = default)
                => Task.FromResult<IReadOnlyList<IEventSubscription>>([]);

            public Task<IReadOnlyList<IEventSubscription>> ResolveSubscriptionsAsync(
                CloudEvent @event,
                EventSubscriptionContext? context,
                CancellationToken cancellationToken = default)
            {
                ReceivedContext = context;
                // Capture the resolved instance NOW while the scope is still live.
                ResolvedInstance = context?.Services?.GetRequiredService<CapturingResolver>();
                return Task.FromResult<IReadOnlyList<IEventSubscription>>([]);
            }
        }

        /// <summary>
        /// A read-only resolver that returns a hard-coded <c>com.example.order.placed</c>
        /// subscription and records its invocations in an external list.
        /// </summary>
        private sealed class ReadOnlyOrderResolver : IEventSubscriptionResolver
        {
            public List<string>? Invoked { get; set; }

            public Task<IReadOnlyList<IEventSubscription>> ResolveSubscriptionsAsync(
                CloudEvent @event, CancellationToken cancellationToken = default)
            {
                if (@event.Type != "com.example.order.placed")
                    return Task.FromResult<IReadOnlyList<IEventSubscription>>([]);

                var list = Invoked;
                IReadOnlyList<IEventSubscription> result =
                [
                    new EventSubscription(
                        EventFilter.ByType("com.example.order.placed"),
                        (_, _) => { list?.Add("readonly-resolver"); return Task.CompletedTask; },
                        "readonly-resolver-sub")
                ];
                return Task.FromResult(result);
            }
        }
    }
}

