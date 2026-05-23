//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Hermodr
{
    /// <summary>
    /// Tests for multiple named/unnamed <see cref="IEventPublisher"/> pipelines
    /// registered in the same DI container.
    /// <list type="bullet">
    ///   <item>Named pipelines resolved via keyed DI (<see cref="IEventPublisher"/>) each
    ///   have independent middleware stacks and channels.</item>
    ///   <item>Multiple unnamed (<c>AddEventPublisher()</c>) registrations follow
    ///   last-registered-wins semantics for the keyed slot, so the last pipeline's
    ///   channels and middleware are the ones used when resolving the default
    ///   <see cref="EventPublisher"/>.</item>
    /// </list>
    /// </summary>
    [Trait("Function", "Publisher")]
    [Trait("Concern", "NamedPublishers")]
    public class NamedPublisherTests
    {
        // ── helpers ────────────────────────────────────────────────────────────

        private static CloudEvent MakeEvent(string type = "test.event") => new()
        {
            Type   = type,
            Source = new Uri("https://example.com"),
            Id     = Guid.NewGuid().ToString("N"),
            Time   = DateTimeOffset.UtcNow,
        };

        /// <summary>
        /// Records every middleware invocation for a given pipeline so we can assert
        /// that different publishers use separate, independent middleware stacks.
        /// </summary>
        private sealed class InvocationLog
        {
            private readonly List<string> _entries = new();
            public IReadOnlyList<string> Entries => _entries;
            public void Add(string entry) => _entries.Add(entry);
        }

        /// <summary>
        /// Middleware that appends "<paramref name="label"/>:before" / "after" to the
        /// <see cref="InvocationLog"/> resolved from the DI container.
        /// </summary>
        private sealed class LabelledMiddleware(string label) : IEventMiddleware
        {
            public async Task InvokeAsync(EventContext ctx, EventPublishDelegate next)
            {
                ctx.Services.GetRequiredService<InvocationLog>().Add($"{label}:before");
                await next(ctx);
                ctx.Services.GetRequiredService<InvocationLog>().Add($"{label}:after");
            }
        }

        // ── Named publishers ───────────────────────────────────────────────────

        /// <summary>Each named pipeline dispatches only to its own channel.</summary>
        [Fact]
        public async Task NamedPublishers_EachPipeline_RoutesToItsOwnChannel()
        {
            var alphaReceived = new List<CloudEvent>();
            var betaReceived  = new List<CloudEvent>();

            var services = new ServiceCollection().AddLogging();
            services.AddEventPublisher("alpha", b => b
                .Configure(o => o.Source = new Uri("https://example.com/alpha"))
                .AddTestChannel(e => alphaReceived.Add(e)));

            services.AddEventPublisher("beta", b => b
                .Configure(o => o.Source = new Uri("https://example.com/beta"))
                .AddTestChannel(e => betaReceived.Add(e)));

            var provider = services.BuildServiceProvider();

            await provider.GetRequiredKeyedService<IEventPublisher>("alpha")
                .PublishEventAsync(MakeEvent("alpha.event"), cancellationToken: TestContext.Current.CancellationToken);

            // Only "alpha" channel should have received the event.
            Assert.Single(alphaReceived);
            Assert.Empty(betaReceived);

            await provider.GetRequiredKeyedService<IEventPublisher>("beta")
                .PublishEventAsync(MakeEvent("beta.event"), cancellationToken: TestContext.Current.CancellationToken);

            // "beta" channel now has one event; "alpha" channel unchanged.
            Assert.Single(alphaReceived);
            Assert.Single(betaReceived);
        }

        /// <summary>
        /// Each named publisher runs only its own middleware, not the middleware
        /// registered for other named publishers.
        /// </summary>
        [Fact]
        public async Task NamedPublishers_EachPipeline_RunsItsOwnMiddleware()
        {
            var log = new InvocationLog();

            var services = new ServiceCollection()
                .AddLogging()
                .AddSingleton(log);

            // "write" pipeline has "write-mw" middleware.
            services.AddEventPublisher("write", b => b
                .Configure(o => o.Source = new Uri("https://example.com"))
                .Use<LabelledMiddleware>("write-mw")
                .AddTestChannel(_ => { }));

            // "read" pipeline has "read-mw" middleware.
            services.AddEventPublisher("read", b => b
                .Configure(o => o.Source = new Uri("https://example.com"))
                .Use<LabelledMiddleware>("read-mw")
                .AddTestChannel(_ => { }));

            var provider = services.BuildServiceProvider();

            await provider.GetRequiredKeyedService<IEventPublisher>("write")
                .PublishEventAsync(MakeEvent(), cancellationToken: TestContext.Current.CancellationToken);

            // Only "write-mw" should have run.
            Assert.Contains("write-mw:before", log.Entries);
            Assert.Contains("write-mw:after",  log.Entries);
            Assert.DoesNotContain("read-mw:before", log.Entries);
            Assert.DoesNotContain("read-mw:after",  log.Entries);

            var countAfterWrite = log.Entries.Count;

            await provider.GetRequiredKeyedService<IEventPublisher>("read")
                .PublishEventAsync(MakeEvent(), cancellationToken: TestContext.Current.CancellationToken);

            // Only "read-mw" should have run for the second call.
            Assert.Contains("read-mw:before", log.Entries);
            Assert.Contains("read-mw:after",  log.Entries);
            // "write-mw" count did NOT increase.
            Assert.Equal(countAfterWrite, log.Entries.Count(e => e.StartsWith("write-mw")));
        }

        /// <summary>
        /// Named publishers resolved via keyed DI are stable singletons — the same
        /// instance is returned on repeated calls.
        /// </summary>
        [Fact]
        public void NamedPublishers_Factory_ReturnsSameInstanceEachTime()
        {
            var services = new ServiceCollection().AddLogging();
            services.AddEventPublisher("stable", b => b
                .Configure(o => o.Source = new Uri("https://example.com"))
                .AddTestChannel(_ => { }));

            var provider = services.BuildServiceProvider();

            var p1 = provider.GetRequiredKeyedService<IEventPublisher>("stable");
            var p2 = provider.GetRequiredKeyedService<IEventPublisher>("stable");

            Assert.NotNull(p1);
            Assert.Same(p1, p2);
        }

        /// <summary>
        /// Each named pipeline uses the options configured specifically for it;
        /// options for one pipeline do not bleed into another.
        /// Events are created WITHOUT a pre-set Source so that the publisher's
        /// configured source is applied by the enrichment stage.
        /// </summary>
        [Fact]
        public async Task NamedPublishers_EachPipeline_UsesItsOwnOptions()
        {
            Uri? alphaSource = null;
            Uri? betaSource  = null;

            var services = new ServiceCollection().AddLogging();

            services.AddEventPublisher("alpha", b => b
                .Configure(o => o.Source = new Uri("https://alpha.example.com"))
                .AddTestChannel(e => alphaSource = e.Source));

            services.AddEventPublisher("beta", b => b
                .Configure(o => o.Source = new Uri("https://beta.example.com"))
                .AddTestChannel(e => betaSource = e.Source));

            var provider = services.BuildServiceProvider();

            // Create events without a Source so the publisher's configured source is applied.
            var noSourceEvent = new CloudEvent { Type = "test.event", Id = Guid.NewGuid().ToString("N") };

            await provider.GetRequiredKeyedService<IEventPublisher>("alpha")
                .PublishEventAsync(noSourceEvent, cancellationToken: TestContext.Current.CancellationToken);

            noSourceEvent = new CloudEvent { Type = "test.event", Id = Guid.NewGuid().ToString("N") };
            await provider.GetRequiredKeyedService<IEventPublisher>("beta")
                .PublishEventAsync(noSourceEvent, cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal(new Uri("https://alpha.example.com"), alphaSource);
            Assert.Equal(new Uri("https://beta.example.com"), betaSource);
        }

        /// <summary>
        /// Named and default publishers co-exist: the default publisher and each named
        /// publisher are completely independent.
        /// </summary>
        [Fact]
        public async Task NamedPublisher_CoexistsWithDefaultPublisher()
        {
            var defaultReceived = new List<CloudEvent>();
            var namedReceived   = new List<CloudEvent>();

            var services = new ServiceCollection().AddLogging();

            services.AddEventPublisher(o =>
                o.Source = new Uri("https://default.example.com"))
                .AddTestChannel(e => defaultReceived.Add(e));

            services.AddEventPublisher("special", b => b
                .Configure(o => o.Source = new Uri("https://special.example.com"))
                .AddTestChannel(e => namedReceived.Add(e)));

            var provider = services.BuildServiceProvider();

            // Default publisher via concrete type.
            await provider.GetRequiredService<EventPublisher>()
                .PublishEventAsync(MakeEvent("default.event"), cancellationToken: TestContext.Current.CancellationToken);

            // Named publisher via keyed service.
            await provider.GetRequiredKeyedService<IEventPublisher>("special")
                .PublishEventAsync(MakeEvent("special.event"), cancellationToken: TestContext.Current.CancellationToken);

            Assert.Single(defaultReceived);
            Assert.Equal("default.event", defaultReceived[0].Type);

            Assert.Single(namedReceived);
            Assert.Equal("special.event", namedReceived[0].Type);
        }

        // ── Multiple unnamed (default) publishers ──────────────────────────────

        /// <summary>
        /// When <see cref="ServiceCollectionExtensions.AddEventPublisher(IServiceCollection)"/> is
        /// called multiple times without a name, the last registration wins: the keyed
        /// slot for the default publisher is overwritten, and the non-keyed aliases
        /// delegate to that keyed slot (last-registered-wins).
        /// </summary>
        [Fact]
        public async Task MultipleDefaultPublishers_LastRegistrationWins()
        {
            var firstReceived  = new List<CloudEvent>();
            var secondReceived = new List<CloudEvent>();

            var services = new ServiceCollection().AddLogging();

            // First registration.
            services.AddEventPublisher(o =>
                o.Source = new Uri("https://first.example.com"))
                .AddTestChannel(e => firstReceived.Add(e));

            // Second registration — replaces the keyed slot for the default publisher.
            services.AddEventPublisher(o =>
                o.Source = new Uri("https://second.example.com"))
                .AddTestChannel(e => secondReceived.Add(e));

            var provider  = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<EventPublisher>();

            await publisher.PublishEventAsync(MakeEvent(), cancellationToken: TestContext.Current.CancellationToken);

            // The second pipeline won — only its channel receives the event.
            Assert.Empty(firstReceived);
            Assert.Single(secondReceived);
        }

        /// <summary>
        /// Resolving a named publisher that was never registered throws
        /// <see cref="InvalidOperationException"/>.
        /// </summary>
        [Fact]
        public void NamedPublisher_UnknownName_Throws()
        {
            var services = new ServiceCollection().AddLogging();
            services.AddEventPublisher("known", b => b
                .Configure(o => o.Source = new Uri("https://example.com"))
                .AddTestChannel(_ => { }));

            var provider = services.BuildServiceProvider();

            Assert.Throws<InvalidOperationException>(
                () => provider.GetRequiredKeyedService<IEventPublisher>("unknown"));
        }

        /// <summary>
        /// Multiple named publishers can each have multiple middleware in their own
        /// ordered stack, and those stacks do not interfere with each other.
        /// </summary>
        [Fact]
        public async Task NamedPublishers_IndependentMiddlewareOrder_IsPreservedPerPipeline()
        {
            var log = new InvocationLog();
            var services = new ServiceCollection()
                .AddLogging()
                .AddSingleton(log);

            // "pipe-a" has [outer-a → inner-a]
            services.AddEventPublisher("pipe-a", b => b
                .Configure(o => o.Source = new Uri("https://example.com"))
                .Use<LabelledMiddleware>("outer-a")
                .Use<LabelledMiddleware>("inner-a")
                .AddTestChannel(_ => { }));

            // "pipe-b" has [outer-b → inner-b] — reversed label order
            services.AddEventPublisher("pipe-b", b => b
                .Configure(o => o.Source = new Uri("https://example.com"))
                .Use<LabelledMiddleware>("outer-b")
                .Use<LabelledMiddleware>("inner-b")
                .AddTestChannel(_ => { }));

            var provider = services.BuildServiceProvider();

            await provider.GetRequiredKeyedService<IEventPublisher>("pipe-a")
                .PublishEventAsync(MakeEvent(), cancellationToken: TestContext.Current.CancellationToken);

            var entriesA = log.Entries.ToList();
            Assert.Equal(["outer-a:before", "inner-a:before", "inner-a:after", "outer-a:after"], entriesA);

            await provider.GetRequiredKeyedService<IEventPublisher>("pipe-b")
                .PublishEventAsync(MakeEvent(), cancellationToken: TestContext.Current.CancellationToken);

            var entriesB = log.Entries.Skip(entriesA.Count).ToList();
            Assert.Equal(["outer-b:before", "inner-b:before", "inner-b:after", "outer-b:after"], entriesB);
        }
    }
}

