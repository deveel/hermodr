//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Diagnostics;

using CloudNative.CloudEvents;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Hermodr;

public class EndToEndTracePropagationTests
{
    private const string TraceParentKey = "traceparent";

    [Fact]
    public async Task PublishAndSubscribe_TraceContextPropagatesEndToEnd()
    {
        await using var fixture = new TestFixture();
        var publisher = fixture.CreatePublisher();

        await publisher.PublishEventAsync(MakeEvent("com.test.order.created"));

        Assert.Equal(2, fixture.CapturedActivities.Count);

        var publishActivity = fixture.CapturedActivities[0];
        var consumeActivity = fixture.CapturedActivities[1];

        Assert.Equal(ActivityKind.Producer, publishActivity.Kind);
        Assert.Equal(ActivityKind.Consumer, consumeActivity.Kind);
        Assert.Equal(publishActivity.TraceId, consumeActivity.TraceId);
        Assert.NotNull(fixture.HandlerActivity);
        Assert.Equal(consumeActivity.TraceId, fixture.HandlerActivity!.TraceId);
    }

    [Fact]
    public async Task PublishAndSubscribe_ProducesCorrectParentChain()
    {
        await using var fixture = new TestFixture();
        var publisher = fixture.CreatePublisher();

        await publisher.PublishEventAsync(MakeEvent());

        var publishActivity = fixture.CapturedActivities[0];
        var consumeActivity = fixture.CapturedActivities[1];

        Assert.Equal(publishActivity.TraceId, consumeActivity.TraceId);
        Assert.Equal(publishActivity.SpanId.ToHexString(), consumeActivity.ParentSpanId.ToHexString());
    }

    [Fact]
    public async Task PublishAndSubscribe_MultipleHandlers_ShareSameTrace()
    {
        await using var fixture = new TestFixture();
        var publisher = fixture.CreatePublisher();

        await publisher.PublishEventAsync(MakeEvent());

        var producerActivities = fixture.CapturedActivities.Where(a => a.Kind == ActivityKind.Producer).ToList();
        var consumerActivities = fixture.CapturedActivities.Where(a => a.Kind == ActivityKind.Consumer).ToList();

        Assert.Single(producerActivities);
        Assert.Single(consumerActivities);

        Assert.Equal(producerActivities[0].TraceId, consumerActivities[0].TraceId);
        Assert.NotNull(fixture.HandlerActivity);
        Assert.Equal(producerActivities[0].TraceId, fixture.HandlerActivity!.TraceId);
    }


    [Fact]
    public async Task PublishAndSubscribe_TraceStatePropagatesEndToEnd()
    {
        await using var fixture = new TestFixture();
        var publisher = fixture.CreatePublisher();

        var cloudEvent = MakeEvent();
        var traceparentAttr = CloudEventAttribute.CreateExtension(TraceParentKey, CloudEventAttributeType.String);
        var traceStateAttr = CloudEventAttribute.CreateExtension("tracestate", CloudEventAttributeType.String);
        cloudEvent[traceparentAttr] = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";
        cloudEvent[traceStateAttr] = "tenant=abc";

        await publisher.PublishEventAsync(cloudEvent);

        var consumeActivity = fixture.CapturedActivities.Single(a => a.Kind == ActivityKind.Consumer);
        Assert.Equal("tenant=abc", consumeActivity.TraceStateString);
    }


    [Fact]
    public async Task PublishAndSubscribe_PublisherOnly_NoConsumerSpan()
    {
        await using var fixture = new TestFixturePublisherOnly();
        var publisher = fixture.CreatePublisher();

        await publisher.PublishEventAsync(MakeEvent());

        Assert.Single(fixture.CapturedActivities);
        Assert.Equal(ActivityKind.Producer, fixture.CapturedActivities[0].Kind);
    }

    [Fact]
    public async Task PublishAndSubscribe_SubscriptionOnly_NoProducerSpan()
    {
        await using var fixture = new TestFixtureSubscriptionOnly();
        var publisher = fixture.CreatePublisher();

        await publisher.PublishEventAsync(MakeEvent());

        Assert.Single(fixture.CapturedActivities);
        Assert.Equal(ActivityKind.Consumer, fixture.CapturedActivities[0].Kind);
    }

    [Fact]
    public async Task PublishAndSubscribe_MultiplePublishs_CreatesSeparateTraces()
    {
        await using var fixture = new TestFixture();
        var publisher = fixture.CreatePublisher();

        await publisher.PublishEventAsync(MakeEvent("com.test.first"));
        await publisher.PublishEventAsync(MakeEvent("com.test.second"));

        var producerActivities = fixture.CapturedActivities.Where(a => a.Kind == ActivityKind.Producer).ToList();
        var consumerActivities = fixture.CapturedActivities.Where(a => a.Kind == ActivityKind.Consumer).ToList();

        Assert.Equal(2, producerActivities.Count);
        Assert.Equal(2, consumerActivities.Count);

        Assert.NotEqual(producerActivities[0].TraceId, producerActivities[1].TraceId);
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

        public List<Activity> CapturedActivities { get; } = new();
        public Activity? HandlerActivity { get; private set; }

        public TestFixture(Action<OpenTelemetryInstrumentationOptions>? configure = null)
        {
            var sourceName = $"Hermodr.Test.{Guid.NewGuid():N}";
            _source = new ActivitySource(sourceName);
            _listener = new ActivityListener
            {
                ShouldListenTo = s => s.Name == sourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
                ActivityStarted = a => CapturedActivities.Add(a),
            };
            ActivitySource.AddActivityListener(_listener);

            var services = new ServiceCollection().AddLogging();
            services.AddSingleton(_source);
            var builder = services.AddEventPublisher(opts => opts.Source = new Uri("https://example.com"));
            builder.UseOpenTelemetry(o =>
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

    private sealed class TestFixturePublisherOnly : IAsyncDisposable
    {
        private readonly ActivityListener _listener;
        private readonly ActivitySource _source;
        private readonly ServiceProvider _provider;

        public List<Activity> CapturedActivities { get; } = new();

        public TestFixturePublisherOnly()
        {
            var sourceName = $"Hermodr.Test.{Guid.NewGuid():N}";
            _source = new ActivitySource(sourceName);
            _listener = new ActivityListener
            {
                ShouldListenTo = s => s.Name == sourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
                ActivityStarted = a => CapturedActivities.Add(a),
            };
            ActivitySource.AddActivityListener(_listener);

            var services = new ServiceCollection().AddLogging();
            services.AddSingleton(_source);
            services.AddEventPublisher(opts => opts.Source = new Uri("https://example.com"))
                .UseOpenTelemetry(o =>
                {
                    o.ActivitySourceName = sourceName;
                    o.InstrumentSubscription = false;
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

    private sealed class TestFixtureSubscriptionOnly : IAsyncDisposable
    {
        private readonly ActivityListener _listener;
        private readonly ActivitySource _source;
        private readonly ServiceProvider _provider;

        public List<Activity> CapturedActivities { get; } = new();

        public TestFixtureSubscriptionOnly()
        {
            var sourceName = $"Hermodr.Test.{Guid.NewGuid():N}";
            _source = new ActivitySource(sourceName);
            _listener = new ActivityListener
            {
                ShouldListenTo = s => s.Name == sourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
                ActivityStarted = a => CapturedActivities.Add(a),
            };
            ActivitySource.AddActivityListener(_listener);

            var services = new ServiceCollection().AddLogging();
            services.AddSingleton(_source);
            services.AddEventPublisher(opts => opts.Source = new Uri("https://example.com"))
                .UseOpenTelemetry(o =>
                {
                    o.ActivitySourceName = sourceName;
                    o.InstrumentPublisher = false;
                })
                .AddSubscriptions(subs =>
                {
                    subs.Subscribe("com.test.*", async (evt, ct) => await Task.CompletedTask);
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
