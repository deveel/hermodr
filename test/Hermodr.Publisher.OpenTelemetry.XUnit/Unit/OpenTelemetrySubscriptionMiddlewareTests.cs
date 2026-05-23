//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Diagnostics;

using CloudNative.CloudEvents;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Hermodr;

public class OpenTelemetrySubscriptionMiddlewareTests
{
    private const string TraceParentKey = "traceparent";

    [Fact]
    public async Task Subscribe_HandlerParticipatesInConsumerSpan()
    {
        await using var fixture = new TestFixture();
        var publisher = fixture.CreatePublisher();

        await publisher.PublishEventAsync(MakeEvent());

        Assert.NotNull(fixture.HandlerActivity);
        Assert.Equal(ActivityKind.Consumer, fixture.HandlerActivity!.Kind);
    }

    [Fact]
    public async Task Subscribe_ExtractsTraceContext_FromEventExtensions()
    {
        await using var fixture = new TestFixture();
        var publisher = fixture.CreatePublisher();

        var cloudEvent = MakeEvent();
        var traceparentAttr = CloudEventAttribute.CreateExtension(TraceParentKey, CloudEventAttributeType.String);
        cloudEvent[traceparentAttr] = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";

        await publisher.PublishEventAsync(cloudEvent);

        var activity = fixture.SingleConsumerActivity;
        Assert.Equal(ActivityTraceId.CreateFromString("4bf92f3577b34da6a3ce929d0e0e4736".AsSpan()), activity.TraceId);
    }

    [Fact]
    public async Task Subscribe_CreatesConsumerSpan()
    {
        await using var fixture = new TestFixture();
        var publisher = fixture.CreatePublisher();

        await publisher.PublishEventAsync(MakeEvent("com.test.order"));

        var activity = fixture.SingleConsumerActivity;
        Assert.Equal("handle com.test.order", activity.DisplayName);
    }

    [Fact]
    public async Task Subscribe_InvokesEnrichWithEvent_Callback()
    {
        Activity? enrichedActivity = null;
        CloudEvent? enrichedEvent = null;

        await using var fixture = new TestFixture(options =>
        {
            options.EnrichWithEvent = (activity, @event) =>
            {
                enrichedActivity = activity;
                enrichedEvent = @event;
            };
        });
        var publisher = fixture.CreatePublisher();

        await publisher.PublishEventAsync(MakeEvent());

        Assert.NotNull(enrichedActivity);
        Assert.NotNull(enrichedEvent);
        Assert.Equal(ActivityKind.Consumer, enrichedActivity!.Kind);
    }

    [Fact]
    public async Task Subscribe_SetsEventIdTag()
    {
        await using var fixture = new TestFixture();
        var publisher = fixture.CreatePublisher();

        var evt = MakeEvent();
        evt.Id = "my-event-id";
        await publisher.PublishEventAsync(evt);

        Assert.Equal("my-event-id", fixture.SingleConsumerActivity.GetTagItem("event.id"));
    }

    [Fact]
    public async Task Subscribe_SetsMessagingSystemTag()
    {
        await using var fixture = new TestFixture();
        var publisher = fixture.CreatePublisher();

        await publisher.PublishEventAsync(MakeEvent());

        var activity = fixture.SingleConsumerActivity;
        Assert.Equal("hermodr", activity.GetTagItem("messaging.system"));
        Assert.Equal("receive", activity.GetTagItem("messaging.operation"));
    }


    [Fact]
    public async Task Subscribe_PropagatesTraceState_FromEventExtensions()
    {
        await using var fixture = new TestFixture();
        var publisher = fixture.CreatePublisher();

        var cloudEvent = MakeEvent();
        var traceparentAttr = CloudEventAttribute.CreateExtension(TraceParentKey, CloudEventAttributeType.String);
        var traceStateAttr = CloudEventAttribute.CreateExtension("tracestate", CloudEventAttributeType.String);
        cloudEvent[traceparentAttr] = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";
        cloudEvent[traceStateAttr] = "tenant=abc,env=prod";

        await publisher.PublishEventAsync(cloudEvent);

        var activity = fixture.SingleConsumerActivity;
        Assert.Equal("tenant=abc,env=prod", activity.TraceStateString);
    }

    [Fact]
    public async Task Subscribe_CreatesChildSpan_WhenTraceParentPresent()
    {
        await using var fixture = new TestFixture();
        var publisher = fixture.CreatePublisher();

        var cloudEvent = MakeEvent();
        var traceparentAttr = CloudEventAttribute.CreateExtension(TraceParentKey, CloudEventAttributeType.String);
        cloudEvent[traceparentAttr] = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";

        await publisher.PublishEventAsync(cloudEvent);

        var activity = fixture.SingleConsumerActivity;
        Assert.Equal("00f067aa0ba902b7", activity.ParentSpanId.ToHexString());
    }

    [Fact]
    public async Task Subscribe_CompletesSpan_WhenHandlerThrows()
    {
        await using var fixture = new TestFixtureWithThrowingHandler();
        var publisher = fixture.CreatePublisher();

        await publisher.PublishEventAsync(MakeEvent());

        var activity = fixture.SingleConsumerActivity;
        Assert.Equal(ActivityKind.Consumer, activity.Kind);
        Assert.True(activity.Duration > TimeSpan.Zero);
    }


    [Fact]
    public void Middleware_Constructor_UsesNullLogger_WhenNotProvided()
    {
        using var source = new ActivitySource("test-null-logger");
        var middleware = new OpenTelemetrySubscriptionMiddleware(source);

        Assert.NotNull(middleware);
    }

    [Fact]
    public void Middleware_Constructor_UsesDefaultOptions_WhenNotProvided()
    {
        using var source = new ActivitySource("test-default-opts");
        var middleware = new OpenTelemetrySubscriptionMiddleware(source);

        Assert.NotNull(middleware);
    }

    private static CloudEvent MakeEvent(string type = "com.test.event")
    {
        return new CloudEvent
        {
            Type = type,
            Source = new Uri("https://example.com"),
            Id = Guid.NewGuid().ToString("N"),
        };
    }

    private sealed class TestFixture : IAsyncDisposable
    {
        private readonly ActivityListener _listener;
        private readonly ActivitySource _source;
        private readonly ServiceProvider _provider;
        private readonly List<Activity> _captured = new();

        public Activity? HandlerActivity { get; private set; }
        public Activity SingleConsumerActivity => _captured.Single(a => a.Kind == ActivityKind.Consumer);

        public TestFixture(Action<OpenTelemetryInstrumentationOptions>? configure = null)
        {
            var sourceName = $"Hermodr.Test.{Guid.NewGuid():N}";
            _source = new ActivitySource(sourceName);
            _listener = new ActivityListener
            {
                ShouldListenTo = s => s.Name == sourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
                ActivityStarted = a => _captured.Add(a),
            };
            ActivitySource.AddActivityListener(_listener);

            var services = new ServiceCollection().AddLogging();
            services.AddSingleton(_source);
            var builder = services.AddEventPublisher(opts => opts.Source = new Uri("https://example.com"));
            builder.AddOpenTelemetrySubscriptionInstrumentation(o =>
            {
                o.ActivitySourceName = sourceName;
                configure?.Invoke(o);
            });
            builder.AddSubscriptions(subs =>
            {
                subs.Subscribe("com.test.*", async (evt, ct) =>
                {
                    HandlerActivity = Activity.Current;
                    await Task.CompletedTask;
                });
            });
            builder.AddTestChannel(_ => { });

            _provider = services.BuildServiceProvider();
        }

        public EventPublisher CreatePublisher() => _provider.GetRequiredService<EventPublisher>();

        public ValueTask DisposeAsync()
        {
            _listener.Dispose();
            _source.Dispose();
            return _provider.DisposeAsync();
        }
    }

    private sealed class TestFixtureWithThrowingHandler : IAsyncDisposable
    {
        private readonly ActivityListener _listener;
        private readonly ActivitySource _source;
        private readonly ServiceProvider _provider;
        private readonly List<Activity> _captured = new();

        public Activity SingleConsumerActivity => _captured.Single(a => a.Kind == ActivityKind.Consumer);

        public TestFixtureWithThrowingHandler()
        {
            var sourceName = $"Hermodr.Test.{Guid.NewGuid():N}";
            _source = new ActivitySource(sourceName);
            _listener = new ActivityListener
            {
                ShouldListenTo = s => s.Name == sourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
                ActivityStarted = a => _captured.Add(a),
            };
            ActivitySource.AddActivityListener(_listener);

            var services = new ServiceCollection().AddLogging();
            services.AddSingleton(_source);
            services.AddEventPublisher(opts => opts.Source = new Uri("https://example.com"))
                .AddOpenTelemetrySubscriptionInstrumentation(o => o.ActivitySourceName = sourceName)
                .AddSubscriptions(subs =>
                {
                    subs.Subscribe("com.test.*", async (evt, ct) => throw new InvalidOperationException("handler error"));
                })
                .AddTestChannel(_ => { });

            _provider = services.BuildServiceProvider();
        }

        public EventPublisher CreatePublisher() => _provider.GetRequiredService<EventPublisher>();

        public ValueTask DisposeAsync()
        {
            _listener.Dispose();
            _source.Dispose();
            return _provider.DisposeAsync();
        }
    }
}
