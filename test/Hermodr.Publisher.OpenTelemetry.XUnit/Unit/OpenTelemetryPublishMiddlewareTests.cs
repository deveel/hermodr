//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Diagnostics;

using CloudNative.CloudEvents;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Hermodr;

public class OpenTelemetryPublishMiddlewareTests
{
    private const string TraceParentKey = "traceparent";

    [Fact]
    public async Task PublishEventAsync_InjectsTraceContextIntoEvent()
    {
        await using var fixture = new TestFixture();
        var publisher = fixture.CreatePublisher();

        await publisher.PublishEventAsync(MakeEvent());

        var traceparentAttr = CloudEventAttribute.CreateExtension(TraceParentKey, CloudEventAttributeType.String);
        var traceparent = fixture.Received[0][traceparentAttr] as string;
        Assert.NotNull(traceparent);
        Assert.StartsWith("00-", traceparent);
    }

    [Fact]
    public async Task PublishEventAsync_CreatesProducerSpan()
    {
        await using var fixture = new TestFixture();
        var publisher = fixture.CreatePublisher();

        await publisher.PublishEventAsync(MakeEvent("com.test.order"));

        var activity = fixture.SingleProducerActivity;
        Assert.Equal("publish com.test.order", activity.DisplayName);
        Assert.Equal(ActivityStatusCode.Ok, activity.Status);
    }

    [Fact]
    public async Task PublishEventAsync_SetsEventIdTag()
    {
        await using var fixture = new TestFixture();
        var publisher = fixture.CreatePublisher();

        var evt = MakeEvent();
        evt.Id = "my-event-id";
        await publisher.PublishEventAsync(evt);

        Assert.Equal("my-event-id", fixture.SingleProducerActivity.GetTagItem("event.id"));
    }

    [Fact]
    public async Task PublishEventAsync_PropagatesException_FromChannel()
    {
        await using var fixture = new TestFixture(throwOnChannel: true);
        var publisher = fixture.CreatePublisher();

        await Assert.ThrowsAsync<EventPublishChannelException>(() => publisher.PublishEventAsync(MakeEvent()));

        Assert.Equal(ActivityStatusCode.Error, fixture.SingleProducerActivity.Status);
    }

    [Fact]
    public async Task PublishEventAsync_InvokesEnrichWithEvent_Callback()
    {
        Activity? enrichedActivity = null;
        CloudEvent? enrichedEvent = null;

        await using var fixture = new TestFixture(configure: options =>
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
        Assert.Equal(ActivityKind.Producer, enrichedActivity!.Kind);
        Assert.Equal("com.test.event", enrichedEvent!.Type);
    }

    [Fact]
    public async Task PublishEventAsync_SetsExceptionDetails_WhenRecordExceptionIsTrue()
    {
        await using var fixture = new TestFixture(throwOnChannel: true, recordException: true);
        var publisher = fixture.CreatePublisher();

        await Assert.ThrowsAsync<EventPublishChannelException>(() => publisher.PublishEventAsync(MakeEvent()));

        var activity = fixture.SingleProducerActivity;
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.NotEmpty(activity.StatusDescription);
    }


    [Fact]
    public async Task PublishEventAsync_UsesDefaultOptions_WhenNotConfigured()
    {
        await using var fixture = new TestFixtureWithoutOptions();
        var publisher = fixture.CreatePublisher();

        await publisher.PublishEventAsync(MakeEvent());

        Assert.Single(fixture.Received);
    }


    [Fact]
    public async Task PublishEventAsync_SetsMessagingSystemTag()
    {
        await using var fixture = new TestFixture();
        var publisher = fixture.CreatePublisher();

        await publisher.PublishEventAsync(MakeEvent());

        var activity = fixture.SingleProducerActivity;
        Assert.Equal("hermodr", activity.GetTagItem("messaging.system"));
        Assert.Equal("publish", activity.GetTagItem("messaging.operation"));
    }

    [Fact]
    public void Middleware_Constructor_UsesNullLogger_WhenNotProvided()
    {
        using var source = new ActivitySource("test-null-logger");
        var middleware = new OpenTelemetryPublishMiddleware(source);

        Assert.NotNull(middleware);
    }

    [Fact]
    public void Middleware_Constructor_UsesDefaultOptions_WhenNotProvided()
    {
        using var source = new ActivitySource("test-default-opts");
        var middleware = new OpenTelemetryPublishMiddleware(source);

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

        public List<CloudEvent> Received { get; } = new();

        public Activity SingleProducerActivity => _captured.Single(a => a.Kind == ActivityKind.Producer);

        public TestFixture(bool throwOnChannel = false, bool recordException = true, Action<OpenTelemetryInstrumentationOptions>? configure = null)
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
            var builder = services.AddEventPublisher(opts =>
            {
                opts.Source = new Uri("https://example.com");
                opts.ThrowOnErrors = throwOnChannel;
            });
            builder.AddOpenTelemetryPublisherInstrumentation(o =>
            {
                o.ActivitySourceName = sourceName;
                o.RecordException = recordException;
                configure?.Invoke(o);
            });
            builder.AddTestChannel(e =>
            {
                if (throwOnChannel)
                    throw new InvalidOperationException("channel error");
                Received.Add(e);
            });
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

    private sealed class TestFixtureWithoutOptions : IAsyncDisposable
    {
        private readonly ActivityListener _listener;
        private readonly ActivitySource _source;
        private readonly ServiceProvider _provider;
        private readonly List<Activity> _captured = new();

        public List<CloudEvent> Received { get; } = new();

        public TestFixtureWithoutOptions()
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
            services.AddOptions<OpenTelemetryInstrumentationOptions>();
            var builder = services.AddEventPublisher(opts => opts.Source = new Uri("https://example.com"));
            builder.AddOpenTelemetryPublisherInstrumentation();
            builder.AddTestChannel(e => Received.Add(e));
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
