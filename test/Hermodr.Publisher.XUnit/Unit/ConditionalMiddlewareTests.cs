//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

using Microsoft.Extensions.DependencyInjection;

namespace Hermodr
{
    /// <summary>
    /// Tests for the conditional middleware pipeline configured on
    /// <see cref="EventPublisherBuilder"/> via
    /// <see cref="EventPublisherBuilder.UseWhen{TMiddleware}(Func{EventContext,bool},object[])"/>.
    /// </summary>
    [Trait("Function", "ConditionalMiddleware")]
    public class ConditionalMiddlewareTests
    {
        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------

        private const string DefaultSource = "https://test.example.com/source";

        private static CloudEvent MakeEvent(string type = "test.event", string? source = null) =>
            new CloudEvent
            {
                Type = type,
                Source = new Uri(source ?? DefaultSource),
                Id = Guid.NewGuid().ToString("N"),
                Time = DateTimeOffset.UtcNow,
            };

        /// <summary>
        /// Builds a DI provider with the given builder configuration and a test channel
        /// that captures the received events.
        /// </summary>
        private static (IServiceProvider Provider, List<CloudEvent> Received) BuildProvider(
            Action<EventPublisherBuilder> configure)
        {
            var received = new List<CloudEvent>();
            var services = new ServiceCollection().AddLogging();
            var builder = services.AddEventPublisher(opts =>
                opts.Source = new Uri(DefaultSource));
            configure(builder);
            builder.AddTestChannel(e => received.Add(e));
            return (services.BuildServiceProvider(), received);
        }

        // ---------------------------------------------------------------
        // Shared spy / log helpers registered in DI
        // ---------------------------------------------------------------

        /// <summary>An ordered execution log appended to by middleware.</summary>
        private sealed class CallLog : List<string> { }

        // ---------------------------------------------------------------
        // Middleware stubs
        // ---------------------------------------------------------------

        /// <summary>Simple logging middleware; writes to <see cref="CallLog"/> from DI.</summary>
        private sealed class LoggingMiddleware : IEventMiddleware
        {
            public async Task InvokeAsync(EventContext context, EventPublishDelegate next)
            {
                context.Services.GetRequiredService<CallLog>().Add("log:before");
                await next(context);
                context.Services.GetRequiredService<CallLog>().Add("log:after");
            }
        }

        /// <summary>Outer logging middleware; writes distinct labels to <see cref="CallLog"/>.</summary>
        private sealed class OuterMiddleware : IEventMiddleware
        {
            public async Task InvokeAsync(EventContext context, EventPublishDelegate next)
            {
                context.Services.GetRequiredService<CallLog>().Add("outer:before");
                await next(context);
                context.Services.GetRequiredService<CallLog>().Add("outer:after");
            }
        }

        /// <summary>
        /// Enrichment middleware that stamps a CloudEvent extension attribute.
        /// </summary>
        private sealed class EnrichmentMiddleware : IEventMiddleware
        {
            public const string AttributeName = "xenriched";

            public Task InvokeAsync(EventContext context, EventPublishDelegate next)
            {
                var attr = CloudEventAttribute.CreateExtension(AttributeName, CloudEventAttributeType.String);
                context.Event[attr] = "yes";
                return next(context);
            }
        }

        /// <summary>Short-circuits the pipeline — the terminal channel is never reached.</summary>
        private sealed class ShortCircuitMiddleware : IEventMiddleware
        {
            public Task InvokeAsync(EventContext context, EventPublishDelegate next)
                => Task.CompletedTask; // intentionally does NOT call next
        }

        /// <summary>
        /// Records a named marker in <see cref="CallLog"/> before and after calling next.
        /// Accepts the marker as a constructor argument so several independent instances
        /// can be registered with different labels.
        /// </summary>
        private sealed class MarkerMiddleware(string marker) : IEventMiddleware
        {
            public async Task InvokeAsync(EventContext context, EventPublishDelegate next)
            {
                context.Services.GetRequiredService<CallLog>().Add($"{marker}:before");
                await next(context);
                context.Services.GetRequiredService<CallLog>().Add($"{marker}:after");
            }
        }

        /// <summary>
        /// Writes an entry to <see cref="CallLog"/> and captures the <see cref="EventContext"/>
        /// in a shared <see cref="ContextSpy"/> so tests can inspect what the middleware saw.
        /// </summary>
        private sealed class ContextSpy
        {
            public EventContext? Captured { get; set; }
        }

        private sealed class SpyMiddleware : IEventMiddleware
        {
            public Task InvokeAsync(EventContext context, EventPublishDelegate next)
            {
                context.Services.GetRequiredService<ContextSpy>().Captured = context;
                context.Services.GetRequiredService<CallLog>().Add("spy");
                return next(context);
            }
        }

        // ---------------------------------------------------------------
        // 1. Predicate always TRUE
        // ---------------------------------------------------------------

        [Fact]
        public async Task UseWhen_PredicateAlwaysTrue_MiddlewareExecutes()
        {
            var callLog = new CallLog();
            var services = new ServiceCollection().AddLogging().AddSingleton(callLog);
            services.AddEventPublisher(opts => opts.Source = new Uri(DefaultSource))
                .UseWhen<LoggingMiddleware>(_ => true)
                .AddTestChannel(_ => callLog.Add("dispatch"));

            await using var provider = services.BuildServiceProvider();

            await provider.GetRequiredService<EventPublisher>()
                .PublishEventAsync(MakeEvent(), cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal(["log:before", "dispatch", "log:after"], callLog);
        }

        // ---------------------------------------------------------------
        // 2. Predicate always FALSE
        // ---------------------------------------------------------------

        [Fact]
        public async Task UseWhen_PredicateAlwaysFalse_MiddlewareIsSkipped()
        {
            var callLog = new CallLog();
            var services = new ServiceCollection().AddLogging().AddSingleton(callLog);
            services.AddEventPublisher(opts => opts.Source = new Uri(DefaultSource))
                .UseWhen<LoggingMiddleware>(_ => false)
                .AddTestChannel(_ => callLog.Add("dispatch"));

            await using var provider = services.BuildServiceProvider();

            await provider.GetRequiredService<EventPublisher>()
                .PublishEventAsync(MakeEvent(), cancellationToken: TestContext.Current.CancellationToken);

            // Middleware labels must be absent; dispatch must still occur.
            Assert.DoesNotContain("log:before", callLog);
            Assert.DoesNotContain("log:after", callLog);
            Assert.Contains("dispatch", callLog);
        }

        // ---------------------------------------------------------------
        // 3. Skipped middleware does NOT prevent channel dispatch
        // ---------------------------------------------------------------

        [Fact]
        public async Task UseWhen_PredicateFalse_EventStillReachesChannel()
        {
            var (provider, received) = BuildProvider(b =>
                b.UseWhen<ShortCircuitMiddleware>(_ => false));

            await using ((IAsyncDisposable)provider)
            {
                await provider.GetRequiredService<EventPublisher>()
                    .PublishEventAsync(MakeEvent(), cancellationToken: TestContext.Current.CancellationToken);
            }

            Assert.Single(received);
        }

        // ---------------------------------------------------------------
        // 4. Predicate based on event type — matched
        // ---------------------------------------------------------------

        [Fact]
        public async Task UseWhen_EventTypeMatchesPredicate_MiddlewareExecutes()
        {
            var callLog = new CallLog();
            var services = new ServiceCollection().AddLogging().AddSingleton(callLog);
            services.AddEventPublisher(opts => opts.Source = new Uri(DefaultSource))
                .UseWhen<LoggingMiddleware>(ctx => ctx.Event.Type == "order.created")
                .AddTestChannel(_ => callLog.Add("dispatch"));

            await using var provider = services.BuildServiceProvider();

            await provider.GetRequiredService<EventPublisher>()
                .PublishEventAsync(MakeEvent("order.created"), cancellationToken: TestContext.Current.CancellationToken);

            Assert.Contains("log:before", callLog);
            Assert.Contains("log:after", callLog);
        }

        // ---------------------------------------------------------------
        // 5. Predicate based on event type — not matched
        // ---------------------------------------------------------------

        [Fact]
        public async Task UseWhen_EventTypeDoesNotMatchPredicate_MiddlewareSkipped()
        {
            var callLog = new CallLog();
            var services = new ServiceCollection().AddLogging().AddSingleton(callLog);
            services.AddEventPublisher(opts => opts.Source = new Uri(DefaultSource))
                .UseWhen<LoggingMiddleware>(ctx => ctx.Event.Type == "order.created")
                .AddTestChannel(_ => callLog.Add("dispatch"));

            await using var provider = services.BuildServiceProvider();

            await provider.GetRequiredService<EventPublisher>()
                .PublishEventAsync(MakeEvent("user.registered"), cancellationToken: TestContext.Current.CancellationToken);

            Assert.DoesNotContain("log:before", callLog);
            Assert.DoesNotContain("log:after", callLog);
            Assert.Contains("dispatch", callLog);
        }

        // ---------------------------------------------------------------
        // 6. Conditional middleware between unconditional ones (matching)
        // ---------------------------------------------------------------

        [Fact]
        public async Task UseWhen_BetweenUnconditionalMiddlewares_RunsInOrder_WhenMatched()
        {
            var callLog = new CallLog();
            var services = new ServiceCollection().AddLogging().AddSingleton(callLog);
            services.AddEventPublisher(opts => opts.Source = new Uri(DefaultSource))
                .Use<OuterMiddleware>()
                .UseWhen<LoggingMiddleware>(ctx => ctx.Event.Type == "order.created")
                .AddTestChannel(_ => callLog.Add("dispatch"));

            await using var provider = services.BuildServiceProvider();

            await provider.GetRequiredService<EventPublisher>()
                .PublishEventAsync(MakeEvent("order.created"), cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal(
                ["outer:before", "log:before", "dispatch", "log:after", "outer:after"],
                callLog);
        }

        // ---------------------------------------------------------------
        // 7. Conditional middleware between unconditional ones (not matching)
        // ---------------------------------------------------------------

        [Fact]
        public async Task UseWhen_BetweenUnconditionalMiddlewares_SkipsConditional_WhenNotMatched()
        {
            var callLog = new CallLog();
            var services = new ServiceCollection().AddLogging().AddSingleton(callLog);
            services.AddEventPublisher(opts => opts.Source = new Uri(DefaultSource))
                .Use<OuterMiddleware>()
                .UseWhen<LoggingMiddleware>(ctx => ctx.Event.Type == "order.created")
                .AddTestChannel(_ => callLog.Add("dispatch"));

            await using var provider = services.BuildServiceProvider();

            await provider.GetRequiredService<EventPublisher>()
                .PublishEventAsync(MakeEvent("user.registered"), cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal(
                ["outer:before", "dispatch", "outer:after"],
                callLog);
        }

        // ---------------------------------------------------------------
        // 8. Multiple UseWhen — all matching
        // ---------------------------------------------------------------

        [Fact]
        public async Task UseWhen_MultipleConditionals_AllMatch_AllExecute()
        {
            var callLog = new CallLog();
            var services = new ServiceCollection().AddLogging().AddSingleton(callLog);
            services.AddEventPublisher(opts => opts.Source = new Uri(DefaultSource))
                .UseWhen<MarkerMiddleware>(ctx => ctx.Event.Type == "order.created", "A")
                .UseWhen<MarkerMiddleware>(ctx => ctx.Event.Type == "order.created", "B")
                .AddTestChannel(_ => callLog.Add("dispatch"));

            await using var provider = services.BuildServiceProvider();

            await provider.GetRequiredService<EventPublisher>()
                .PublishEventAsync(MakeEvent("order.created"), cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal(
                ["A:before", "B:before", "dispatch", "B:after", "A:after"],
                callLog);
        }

        // ---------------------------------------------------------------
        // 9. Multiple UseWhen — none matching
        // ---------------------------------------------------------------

        [Fact]
        public async Task UseWhen_MultipleConditionals_NoneMatch_AllSkipped_DispatchStillOccurs()
        {
            var callLog = new CallLog();
            var services = new ServiceCollection().AddLogging().AddSingleton(callLog);
            services.AddEventPublisher(opts => opts.Source = new Uri(DefaultSource))
                .UseWhen<MarkerMiddleware>(ctx => ctx.Event.Type == "order.created", "A")
                .UseWhen<MarkerMiddleware>(ctx => ctx.Event.Type == "order.created", "B")
                .AddTestChannel(_ => callLog.Add("dispatch"));

            await using var provider = services.BuildServiceProvider();

            await provider.GetRequiredService<EventPublisher>()
                .PublishEventAsync(MakeEvent("user.registered"), cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal(["dispatch"], callLog);
        }

        // ---------------------------------------------------------------
        // 10. Multiple UseWhen — partial match (first matches, second does not)
        // ---------------------------------------------------------------

        [Fact]
        public async Task UseWhen_MultipleConditionals_PartialMatch_OnlyMatchingExecutes()
        {
            var callLog = new CallLog();
            var services = new ServiceCollection().AddLogging().AddSingleton(callLog);
            services.AddEventPublisher(opts => opts.Source = new Uri(DefaultSource))
                .UseWhen<MarkerMiddleware>(ctx => ctx.Event.Type == "order.created", "A")
                .UseWhen<MarkerMiddleware>(ctx => ctx.Event.Type == "user.registered", "B")
                .AddTestChannel(_ => callLog.Add("dispatch"));

            await using var provider = services.BuildServiceProvider();

            await provider.GetRequiredService<EventPublisher>()
                .PublishEventAsync(MakeEvent("order.created"), cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal(["A:before", "dispatch", "A:after"], callLog);
            Assert.DoesNotContain("B:before", callLog);
        }

        // ---------------------------------------------------------------
        // 11. Conditional enrichment middleware — attribute present when matched
        // ---------------------------------------------------------------

        [Fact]
        public async Task UseWhen_ConditionalEnrichment_AttributePresent_WhenPredicateMatches()
        {
            CloudEvent? dispatched = null;
            var services = new ServiceCollection().AddLogging();
            services.AddEventPublisher(opts => opts.Source = new Uri(DefaultSource))
                .UseWhen<EnrichmentMiddleware>(ctx => ctx.Event.Type == "order.created")
                .AddTestChannel(e => dispatched = e);

            await using var provider = services.BuildServiceProvider();

            await provider.GetRequiredService<EventPublisher>()
                .PublishEventAsync(MakeEvent("order.created"), cancellationToken: TestContext.Current.CancellationToken);

            Assert.NotNull(dispatched);
            Assert.Equal("yes", dispatched![EnrichmentMiddleware.AttributeName]);
        }

        // ---------------------------------------------------------------
        // 12. Conditional enrichment middleware — attribute absent when not matched
        // ---------------------------------------------------------------

        [Fact]
        public async Task UseWhen_ConditionalEnrichment_AttributeAbsent_WhenPredicateDoesNotMatch()
        {
            CloudEvent? dispatched = null;
            var services = new ServiceCollection().AddLogging();
            services.AddEventPublisher(opts => opts.Source = new Uri(DefaultSource))
                .UseWhen<EnrichmentMiddleware>(ctx => ctx.Event.Type == "order.created")
                .AddTestChannel(e => dispatched = e);

            await using var provider = services.BuildServiceProvider();

            await provider.GetRequiredService<EventPublisher>()
                .PublishEventAsync(MakeEvent("user.registered"), cancellationToken: TestContext.Current.CancellationToken);

            Assert.NotNull(dispatched);
            Assert.Null(dispatched![EnrichmentMiddleware.AttributeName]);
        }

        // ---------------------------------------------------------------
        // 13. Predicate based on Items bag (populated by earlier middleware)
        // ---------------------------------------------------------------

        [Fact]
        public async Task UseWhen_PredicateInspectingItemsBag_SkipsWhenKeyAbsent()
        {
            var callLog = new CallLog();
            var services = new ServiceCollection().AddLogging().AddSingleton(callLog);
            services.AddEventPublisher(opts => opts.Source = new Uri(DefaultSource))
                // UseWhen that checks an Items entry populated by a preceding middleware
                .UseWhen<LoggingMiddleware>(ctx => ctx.Items.ContainsKey("tenant"))
                .AddTestChannel(_ => callLog.Add("dispatch"));

            await using var provider = services.BuildServiceProvider();

            await provider.GetRequiredService<EventPublisher>()
                .PublishEventAsync(MakeEvent(), cancellationToken: TestContext.Current.CancellationToken);

            Assert.DoesNotContain("log:before", callLog);
            Assert.Contains("dispatch", callLog);
        }

        [Fact]
        public async Task UseWhen_PredicateInspectingItemsBag_RunsWhenKeyPresentFromPriorMiddleware()
        {
            var callLog = new CallLog();
            var services = new ServiceCollection().AddLogging().AddSingleton(callLog);

            // First middleware plants an Items entry; second (conditional) middleware reads it.
            services.AddEventPublisher(opts => opts.Source = new Uri(DefaultSource))
                .Use<ItemsPopulatingMiddleware>()
                .UseWhen<LoggingMiddleware>(ctx => ctx.Items.ContainsKey("tenant"))
                .AddTestChannel(_ => callLog.Add("dispatch"));

            await using var provider = services.BuildServiceProvider();

            await provider.GetRequiredService<EventPublisher>()
                .PublishEventAsync(MakeEvent(), cancellationToken: TestContext.Current.CancellationToken);

            Assert.Contains("log:before", callLog);
            Assert.Contains("log:after", callLog);
        }

        /// <summary>Plants a "tenant" key in <see cref="EventContext.Items"/> and passes through.</summary>
        private sealed class ItemsPopulatingMiddleware : IEventMiddleware
        {
            public Task InvokeAsync(EventContext context, EventPublishDelegate next)
            {
                context.Items["tenant"] = "acme";
                return next(context);
            }
        }

        // ---------------------------------------------------------------
        // 14. Short-circuit middleware registered with UseWhen — when predicate
        //     matches, channel is NOT reached
        // ---------------------------------------------------------------

        [Fact]
        public async Task UseWhen_ShortCircuit_WhenMatched_ChannelNotReached()
        {
            var (provider, received) = BuildProvider(b =>
                b.UseWhen<ShortCircuitMiddleware>(ctx => ctx.Event.Type == "blocked.event"));

            await using ((IAsyncDisposable)provider)
            {
                await provider.GetRequiredService<EventPublisher>()
                    .PublishEventAsync(MakeEvent("blocked.event"), cancellationToken: TestContext.Current.CancellationToken);
            }

            Assert.Empty(received);
        }

        [Fact]
        public async Task UseWhen_ShortCircuit_WhenNotMatched_ChannelIsReached()
        {
            var (provider, received) = BuildProvider(b =>
                b.UseWhen<ShortCircuitMiddleware>(ctx => ctx.Event.Type == "blocked.event"));

            await using ((IAsyncDisposable)provider)
            {
                await provider.GetRequiredService<EventPublisher>()
                    .PublishEventAsync(MakeEvent("allowed.event"), cancellationToken: TestContext.Current.CancellationToken);
            }

            Assert.Single(received);
        }

        // ---------------------------------------------------------------
        // 15. Pipeline is re-evaluated per call — different events on the same
        //     publisher produce different execution paths
        // ---------------------------------------------------------------

        [Fact]
        public async Task UseWhen_SamePublisher_DifferentEvents_PredicateEvaluatedPerCall()
        {
            var callLog = new CallLog();
            var services = new ServiceCollection().AddLogging().AddSingleton(callLog);
            services.AddEventPublisher(opts => opts.Source = new Uri(DefaultSource))
                .UseWhen<LoggingMiddleware>(ctx => ctx.Event.Type == "order.created")
                .AddTestChannel(_ => callLog.Add("dispatch"));

            await using var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<EventPublisher>();

            await publisher.PublishEventAsync(MakeEvent("order.created"), cancellationToken: TestContext.Current.CancellationToken);
            await publisher.PublishEventAsync(MakeEvent("user.registered"), cancellationToken: TestContext.Current.CancellationToken);
            await publisher.PublishEventAsync(MakeEvent("order.created"), cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal(
                [
                    "log:before", "dispatch", "log:after",  // 1st call — matched
                    "dispatch",                              // 2nd call — skipped
                    "log:before", "dispatch", "log:after",  // 3rd call — matched
                ],
                callLog);
        }

        // ---------------------------------------------------------------
        // 16. MiddlewareRegistration.IsConditional reflects registration type
        // ---------------------------------------------------------------

        /// <summary>
        /// Creates a <see cref="MiddlewareRegistration"/> without relying on
        /// <c>InternalsVisibleTo</c>: the constructor is internal so we use
        /// <see cref="Activator.CreateInstance(Type, System.Reflection.BindingFlags, System.Reflection.Binder, object[], System.Globalization.CultureInfo)"/>
        /// with <see cref="System.Reflection.BindingFlags.NonPublic"/> to invoke it via reflection.
        /// </summary>
        private static MiddlewareRegistration CreateRegistration(
            Type middlewareType,
            object[] activationArguments,
            Func<EventContext, bool>? predicate)
            => (MiddlewareRegistration)Activator.CreateInstance(
                typeof(MiddlewareRegistration),
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
                binder: null,
                args: [middlewareType, activationArguments, predicate],
                culture: null)!;

        [Fact]
        public void MiddlewareRegistration_IsConditional_FalseWhenNoPredicate()
        {
            var reg = CreateRegistration(typeof(LoggingMiddleware), [], predicate: null);
            Assert.False(reg.IsConditional);
            Assert.Null(reg.Predicate);
        }

        [Fact]
        public void MiddlewareRegistration_IsConditional_TrueWhenPredicateProvided()
        {
            Func<EventContext, bool> pred = _ => true;
            var reg = CreateRegistration(typeof(LoggingMiddleware), [], predicate: pred);
            Assert.True(reg.IsConditional);
            Assert.Same(pred, reg.Predicate);
        }

        // ---------------------------------------------------------------
        // 17. Null predicate argument rejected at builder level
        // ---------------------------------------------------------------

        [Fact]
        public void UseWhen_NullPredicate_ThrowsArgumentNullException()
        {
            var services = new ServiceCollection();
            var builder = services.AddEventPublisher();
            Assert.Throws<ArgumentNullException>(() =>
                builder.UseWhen<LoggingMiddleware>(null!));
        }

        // ---------------------------------------------------------------
        // 18. UseWhen with activation arguments — args forwarded correctly
        //     (predicate matches)
        // ---------------------------------------------------------------

        [Fact]
        public async Task UseWhen_WithActivationArguments_ArgsForwardedWhenPredicateMatches()
        {
            var callLog = new CallLog();
            var services = new ServiceCollection().AddLogging().AddSingleton(callLog);
            services.AddEventPublisher(opts => opts.Source = new Uri(DefaultSource))
                .UseWhen<MarkerMiddleware>(ctx => ctx.Event.Type == "order.created", "custom")
                .AddTestChannel(_ => callLog.Add("dispatch"));

            await using var provider = services.BuildServiceProvider();

            await provider.GetRequiredService<EventPublisher>()
                .PublishEventAsync(MakeEvent("order.created"), cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal(["custom:before", "dispatch", "custom:after"], callLog);
        }

        // ---------------------------------------------------------------
        // 19. UseWhen combined with Use — registration order preserved
        // ---------------------------------------------------------------

        [Fact]
        public async Task UseWhen_MixedWithUse_ExecutionOrderFollowsRegistrationOrder()
        {
            var callLog = new CallLog();
            var services = new ServiceCollection().AddLogging().AddSingleton(callLog);
            services.AddEventPublisher(opts => opts.Source = new Uri(DefaultSource))
                .Use<OuterMiddleware>()                                          // always runs
                .UseWhen<LoggingMiddleware>(ctx => ctx.Event.Type == "x")       // conditional
                .AddTestChannel(_ => callLog.Add("dispatch"));

            await using var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<EventPublisher>();

            // First call: condition met
            await publisher.PublishEventAsync(MakeEvent("x"), cancellationToken: TestContext.Current.CancellationToken);
            // Second call: condition not met
            await publisher.PublishEventAsync(MakeEvent("y"), cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal(
                [
                    "outer:before", "log:before", "dispatch", "log:after", "outer:after",   // x
                    "outer:before", "dispatch", "outer:after",                               // y
                ],
                callLog);
        }

        // ---------------------------------------------------------------
        // 20. Spy middleware: EventContext received by conditional middleware
        //     is the same instance as the one flowing through the pipeline
        // ---------------------------------------------------------------

        [Fact]
        public async Task UseWhen_MiddlewareReceivesCorrectEventContext_WhenPredicateMatches()
        {
            var callLog = new CallLog();
            var spy = new ContextSpy();
            var services = new ServiceCollection()
                .AddLogging()
                .AddSingleton(callLog)
                .AddSingleton(spy);

            services.AddEventPublisher(opts => opts.Source = new Uri(DefaultSource))
                .UseWhen<SpyMiddleware>(ctx => ctx.Event.Type == "order.created")
                .AddTestChannel(_ => { });

            await using var provider = services.BuildServiceProvider();

            var evt = MakeEvent("order.created");
            await provider.GetRequiredService<EventPublisher>()
                .PublishEventAsync(evt, cancellationToken: TestContext.Current.CancellationToken);

            Assert.NotNull(spy.Captured);
            Assert.Equal("order.created", spy.Captured!.Event.Type);
            Assert.Contains("spy", callLog);
        }

        [Fact]
        public async Task UseWhen_ContextNotCaptured_WhenPredicateDoesNotMatch()
        {
            var callLog = new CallLog();
            var spy = new ContextSpy();
            var services = new ServiceCollection()
                .AddLogging()
                .AddSingleton(callLog)
                .AddSingleton(spy);

            services.AddEventPublisher(opts => opts.Source = new Uri(DefaultSource))
                .UseWhen<SpyMiddleware>(ctx => ctx.Event.Type == "order.created")
                .AddTestChannel(_ => { });

            await using var provider = services.BuildServiceProvider();

            await provider.GetRequiredService<EventPublisher>()
                .PublishEventAsync(MakeEvent("user.registered"), cancellationToken: TestContext.Current.CancellationToken);

            Assert.Null(spy.Captured);
            Assert.DoesNotContain("spy", callLog);
        }
    }
}



