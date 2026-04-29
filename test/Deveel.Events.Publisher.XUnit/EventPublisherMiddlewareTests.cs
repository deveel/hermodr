//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using CloudNative.CloudEvents;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Deveel.Events
{
    /// <summary>
    /// Tests for the composable middleware pipeline configured on
    /// <see cref="EventPublisher"/> via <see cref="EventPublisher.Use{TMiddleware}"/>.
    /// </summary>
    [Trait("Function", "Middleware")]
    public class EventPublisherMiddlewareTests
    {
        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------

        private static CloudEvent MakeEvent(string type = "test.event") =>
            new CloudEvent
            {
                Type = type,
                Source = new Uri("https://test.example.com/source"),
                Id = Guid.NewGuid().ToString("N"),
                Time = DateTimeOffset.UtcNow,
            };

        /// <summary>Builds a minimal DI provider with an EventPublisher and a test channel.</summary>
        private static (IServiceProvider Provider, List<CloudEvent> Received) BuildProvider(
            Action<EventPublisherBuilder>? configure = null)
        {
            var received = new List<CloudEvent>();
            var services = new ServiceCollection().AddLogging();
            var builder = services.AddEventPublisher(opts =>
                opts.Source = new Uri("https://test.example.com/source"));
            configure?.Invoke(builder);
            builder.AddTestChannel(e => received.Add(e));
            return (services.BuildServiceProvider(), received);
        }

        // ---------------------------------------------------------------
        // Shared spy / log services (registered in DI; resolved by middleware
        // via ctx.Services so middleware itself stays stateless)
        // ---------------------------------------------------------------

        /// <summary>
        /// A call log that middleware appends entries to.
        /// Registered as a singleton in DI and resolved by middleware via ctx.Services.
        /// </summary>
        private sealed class CallLog : List<string> { }

        /// <summary>Captures the <see cref="EventContext"/> for assertions.</summary>
        private sealed class ContextSpy
        {
            public EventContext? CapturedContext { get; set; }
        }

        // ---------------------------------------------------------------
        // Stateless middleware stubs — no instance fields; state flows via DI.
        // ---------------------------------------------------------------

        private class LoggingMiddleware : IEventMiddleware
        {
            public async Task InvokeAsync(EventContext context, EventPublishDelegate next)
            {
                var log = context.Services.GetRequiredService<CallLog>();
                log.Add("mw:before");
                await next(context);
                log.Add("mw:after");
            }
        }

        private class OuterLoggingMiddleware : IEventMiddleware
        {
            public async Task InvokeAsync(EventContext context, EventPublishDelegate next)
            {
                var log = context.Services.GetRequiredService<CallLog>();
                log.Add("outer:before");
                await next(context);
                log.Add("outer:after");
            }
        }

        private class EnrichmentMiddleware : IEventMiddleware
        {
            public const string AttributeName = "xenriched";

            public Task InvokeAsync(EventContext context, EventPublishDelegate next)
            {
                var attr = CloudEventAttribute.CreateExtension(AttributeName, CloudEventAttributeType.String);
                context.Event[attr] = "yes";
                return next(context);
            }
        }

        private class ShortCircuitMiddleware : IEventMiddleware
        {
            public Task InvokeAsync(EventContext context, EventPublishDelegate next)
                => Task.CompletedTask;
        }

        private class CapturingMiddleware : IEventMiddleware
        {
            public Task InvokeAsync(EventContext context, EventPublishDelegate next)
            {
                var spy = context.Services.GetRequiredService<ContextSpy>();
                spy.CapturedContext = context;
                return next(context);
            }
        }

        private sealed class ParameterizedLoggingMiddleware(string marker) : IEventMiddleware
        {
            public async Task InvokeAsync(EventContext context, EventPublishDelegate next)
            {
                var log = context.Services.GetRequiredService<CallLog>();
                log.Add($"{marker}:before");
                await next(context);
                log.Add($"{marker}:after");
            }
        }

        // ---------------------------------------------------------------
        // Tests
        // ---------------------------------------------------------------

        [Fact]
        public async Task NoMiddleware_PublishesEventToChannel()
        {
            var (provider, received) = BuildProvider();
            await using var _ = (AsyncServiceScope)provider.CreateAsyncScope();

            var publisher = provider.GetRequiredService<EventPublisher>();
            // No Use calls — direct dispatch.

            await publisher.PublishEventAsync(MakeEvent(), cancellationToken: TestContext.Current.CancellationToken);

            Assert.Single(received);
        }

        [Fact]
        public async Task SingleMiddleware_IsInvokedAroundChannelDispatch()
        {
            var callLog = new CallLog();
            var services = new ServiceCollection().AddLogging().AddSingleton(callLog);
            services.AddEventPublisher(opts =>
                    opts.Source = new Uri("https://test.example.com/source"))
                .AddTestChannel(e => { callLog.Add("dispatch"); });

            await using var provider = services.BuildServiceProvider();

            var publisher = provider.GetRequiredService<EventPublisher>()
                .Use<LoggingMiddleware>();

            await publisher.PublishEventAsync(MakeEvent(), cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal(["mw:before", "dispatch", "mw:after"], callLog);
        }

        [Fact]
        public async Task MultipleMiddlewares_RunInRegistrationOrder()
        {
            var callLog = new CallLog();
            var services = new ServiceCollection().AddLogging().AddSingleton(callLog);
            services.AddEventPublisher(opts =>
                    opts.Source = new Uri("https://test.example.com/source"))
                .AddTestChannel(_ => callLog.Add("dispatch"));

            await using var provider = services.BuildServiceProvider();

            // Registration order: Outer first → outermost in chain.
            provider.GetRequiredService<EventPublisher>()
                .Use<OuterLoggingMiddleware>()
                .Use<LoggingMiddleware>();

            var publisher = provider.GetRequiredService<EventPublisher>();
            await publisher.PublishEventAsync(MakeEvent(), cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal(
                ["outer:before", "mw:before", "dispatch", "mw:after", "outer:after"],
                callLog);
        }

        [Fact]
        public async Task EnrichmentMiddleware_ModifiesEventBeforeDispatch()
        {
            CloudEvent? dispatched = null;
            var services = new ServiceCollection().AddLogging();
            services.AddEventPublisher(opts =>
                    opts.Source = new Uri("https://test.example.com/source"))
                .AddTestChannel(e => dispatched = e);

            await using var provider = services.BuildServiceProvider();

            await provider.GetRequiredService<EventPublisher>()
                .Use<EnrichmentMiddleware>()
                .PublishEventAsync(MakeEvent(), cancellationToken: TestContext.Current.CancellationToken);

            Assert.NotNull(dispatched);
            Assert.Equal("yes", dispatched![EnrichmentMiddleware.AttributeName]);
        }

        [Fact]
        public async Task ShortCircuitMiddleware_PreventChannelDispatch()
        {
            var received = new List<CloudEvent>();
            var services = new ServiceCollection().AddLogging();
            services.AddEventPublisher(opts =>
                    opts.Source = new Uri("https://test.example.com/source"))
                .AddTestChannel(e => received.Add(e));

            await using var provider = services.BuildServiceProvider();

            await provider.GetRequiredService<EventPublisher>()
                .Use<ShortCircuitMiddleware>()
                .PublishEventAsync(MakeEvent(), cancellationToken: TestContext.Current.CancellationToken);

            Assert.Empty(received);
        }

        [Fact]
        public async Task Middleware_ReceivesServicesFromDI()
        {
            var spy = new ContextSpy();
            var services = new ServiceCollection().AddLogging().AddSingleton(spy);
            services.AddEventPublisher(opts =>
            {
                opts.Source = new Uri("https://test.example.com/source");
                opts.Attributes["region"] = "eu-west-1";
            })
            .AddTestChannel(_ => { });

            await using var provider = services.BuildServiceProvider();

            var evt = new CloudEvent { Type = "test.event" };
            await provider.GetRequiredService<EventPublisher>()
                .Use<CapturingMiddleware>()
                .PublishEventAsync(evt, cancellationToken: TestContext.Current.CancellationToken);

            Assert.NotNull(spy.CapturedContext);
            Assert.Same(spy, spy.CapturedContext!.Services.GetRequiredService<ContextSpy>());
            Assert.NotNull(spy.CapturedContext.Event.Id);
            Assert.NotNull(spy.CapturedContext.Event.Source);
            Assert.Equal("eu-west-1", spy.CapturedContext.Event["region"]);
        }

        [Fact]
        public async Task Context_CancellationToken_IsForwardedFromPublishCall()
        {
            var spy = new ContextSpy();
            var services = new ServiceCollection().AddLogging().AddSingleton(spy);
            services.AddEventPublisher(opts =>
                    opts.Source = new Uri("https://test.example.com/source"))
                .AddTestChannel(_ => { });

            await using var provider = services.BuildServiceProvider();
            using var cts = new CancellationTokenSource();

            await provider.GetRequiredService<EventPublisher>()
                .Use<CapturingMiddleware>()
                .PublishEventAsync(MakeEvent(), cancellationToken: cts.Token);

            Assert.NotNull(spy.CapturedContext);
            Assert.Equal(cts.Token, spy.CapturedContext!.CancellationToken);
        }

        [Fact]
        public async Task UseMiddleware_OnPublisherInstance_NotOnBuilder()
        {
            // Confirm the builder no longer has Use; middleware is configured
            // on the resolved publisher instance only.
            var callLog = new CallLog();
            var services = new ServiceCollection().AddLogging().AddSingleton(callLog);
            // Builder intentionally has NO Use call.
            services.AddEventPublisher(opts =>
                    opts.Source = new Uri("https://test.example.com/source"))
                .AddTestChannel(_ => callLog.Add("dispatch"));

            await using var provider = services.BuildServiceProvider();
            // Middleware is added directly on the publisher instance.
            var publisher = provider.GetRequiredService<EventPublisher>()
                .Use<LoggingMiddleware>();

            await publisher.PublishEventAsync(MakeEvent(), cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal(["mw:before", "dispatch", "mw:after"], callLog);
        }

        [Fact]
        public async Task Middleware_IsStateless_FreshInstancePerCall()
        {
            var callLog = new CallLog();
            var services = new ServiceCollection().AddLogging().AddSingleton(callLog);
            services.AddEventPublisher(opts =>
                    opts.Source = new Uri("https://test.example.com/source"))
                .AddTestChannel(_ => callLog.Add("dispatch"));

            await using var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<EventPublisher>()
                .Use<LoggingMiddleware>();

            await publisher.PublishEventAsync(MakeEvent(), cancellationToken: TestContext.Current.CancellationToken);
            await publisher.PublishEventAsync(MakeEvent(), cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal(
                ["mw:before", "dispatch", "mw:after", "mw:before", "dispatch", "mw:after"],
                callLog);
        }

        [Fact]
        public async Task Pipeline_IsCachedAcrossMultipleCalls()
        {
            var received = new List<CloudEvent>();
            var services = new ServiceCollection().AddLogging();
            services.AddEventPublisher(opts =>
                    opts.Source = new Uri("https://test.example.com/source"))
                .AddTestChannel(e => received.Add(e));

            await using var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<EventPublisher>()
                .Use<EnrichmentMiddleware>();

            await publisher.PublishEventAsync(MakeEvent(), cancellationToken: TestContext.Current.CancellationToken);
            await publisher.PublishEventAsync(MakeEvent(), cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal(2, received.Count);
            Assert.All(received, e => Assert.Equal("yes", e[EnrichmentMiddleware.AttributeName]));
        }

        [Fact]
        public async Task UseMiddleware_WithActivationArguments_ForwardsArgumentsToConstructor()
        {
            var callLog = new CallLog();
            var services = new ServiceCollection().AddLogging().AddSingleton(callLog);
            services.AddEventPublisher(opts =>
                    opts.Source = new Uri("https://test.example.com/source"))
                .AddTestChannel(_ => callLog.Add("dispatch"));

            await using var provider = services.BuildServiceProvider();
            var publisher = provider.GetRequiredService<EventPublisher>()
                .Use<ParameterizedLoggingMiddleware>("custom");

            await publisher.PublishEventAsync(MakeEvent(), cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal(["custom:before", "dispatch", "custom:after"], callLog);
        }
    }
}


