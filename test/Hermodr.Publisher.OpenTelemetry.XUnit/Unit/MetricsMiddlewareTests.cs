//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Diagnostics;
using System.Diagnostics.Metrics;

using CloudNative.CloudEvents;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Hermodr;

public class MetricsMiddlewareTests
{
    [Fact]
    public async Task PublishEventAsync_RecordsPublishTotal()
    {
        await using var fixture = new MetricsTestFixture();
        var publisher = fixture.CreatePublisher();

        await publisher.PublishEventAsync(MakeEvent());

        Assert.Equal(1, fixture.PublishTotalCount);
    }

    [Fact]
    public async Task PublishEventAsync_RecordsPublishDuration()
    {
        await using var fixture = new MetricsTestFixture();
        var publisher = fixture.CreatePublisher();

        await publisher.PublishEventAsync(MakeEvent());

        Assert.NotEmpty(fixture.PublishDurationValues);
        Assert.All(fixture.PublishDurationValues, v => Assert.True(v >= 0));
    }

    [Fact]
    public async Task PublishEventAsync_RecordsDurationWithSuccessTag()
    {
        await using var fixture = new MetricsTestFixture();
        var publisher = fixture.CreatePublisher();

        await publisher.PublishEventAsync(MakeEvent());

        Assert.Single(fixture.PublishDurationTags);
        var tags = fixture.PublishDurationTags[0];
        Assert.Contains(tags, kvp => kvp.Key == "success" && kvp.Value?.Equals(true) == true);
    }

    [Fact]
    public async Task PublishEventAsync_RecordsErrorCount_OnChannelFailure()
    {
        await using var fixture = new MetricsTestFixture(throwOnChannel: true);
        var publisher = fixture.CreatePublisher();

        await Assert.ThrowsAsync<EventPublishChannelException>(() => publisher.PublishEventAsync(MakeEvent()));

        Assert.Equal(1, fixture.PublishErrorCount);
    }

    [Fact]
    public async Task PublishEventAsync_RecordsDurationWithFailureTag_OnChannelFailure()
    {
        await using var fixture = new MetricsTestFixture(throwOnChannel: true);
        var publisher = fixture.CreatePublisher();

        await Assert.ThrowsAsync<EventPublishChannelException>(() => publisher.PublishEventAsync(MakeEvent()));

        Assert.Single(fixture.PublishDurationTags);
        var tags = fixture.PublishDurationTags[0];
        Assert.Contains(tags, kvp => kvp.Key == "success" && kvp.Value?.Equals(false) == true);
    }

    [Fact]
    public async Task PublishEventAsync_SkipsMetrics_WhenMetricsDisabled()
    {
        await using var fixture = new MetricsTestFixture(configure: opts => opts.Metrics.Enabled = false);
        var publisher = fixture.CreatePublisher();

        await publisher.PublishEventAsync(MakeEvent());

        Assert.Equal(0, fixture.PublishTotalCount);
        Assert.Empty(fixture.PublishDurationValues);
        Assert.Equal(0, fixture.PublishErrorCount);
    }

    [Fact]
    public async Task PublishEventAsync_SkipsPublishDuration_WhenToggleOff()
    {
        await using var fixture = new MetricsTestFixture(configure: opts => opts.Metrics.PublishDuration = false);
        var publisher = fixture.CreatePublisher();

        await publisher.PublishEventAsync(MakeEvent());

        Assert.Empty(fixture.PublishDurationValues);
        Assert.Equal(1, fixture.PublishTotalCount);
    }

    [Fact]
    public async Task PublishEventAsync_SkipsPublishTotal_WhenToggleOff()
    {
        await using var fixture = new MetricsTestFixture(configure: opts => opts.Metrics.PublishTotal = false);
        var publisher = fixture.CreatePublisher();

        await publisher.PublishEventAsync(MakeEvent());

        Assert.Equal(0, fixture.PublishTotalCount);
        Assert.NotEmpty(fixture.PublishDurationValues);
    }

    [Fact]
    public async Task PublishEventAsync_SkipsPublishErrors_WhenToggleOff()
    {
        await using var fixture = new MetricsTestFixture(
            throwOnChannel: true,
            configure: opts => opts.Metrics.PublishErrors = false);
        var publisher = fixture.CreatePublisher();

        await Assert.ThrowsAsync<EventPublishChannelException>(() => publisher.PublishEventAsync(MakeEvent()));

        Assert.Equal(0, fixture.PublishErrorCount);
    }

    [Fact]
    public async Task PublishEventAsync_RecordsEventTypeTag()
    {
        await using var fixture = new MetricsTestFixture();
        var publisher = fixture.CreatePublisher();

        await publisher.PublishEventAsync(MakeEvent("com.test.order"));

        Assert.Single(fixture.PublishTotalTags);
        var tags = fixture.PublishTotalTags[0];
        Assert.Contains(tags, kvp => kvp.Key == "event.type" && kvp.Value?.Equals("com.test.order") == true);
    }

    [Fact]
    public void MetricsOptions_HasCorrectDefaults()
    {
        var options = new MetricsOptions();

        Assert.True(options.Enabled);
        Assert.Equal("Hermodr", options.MeterName);
        Assert.True(options.PublishDuration);
        Assert.True(options.PublishTotal);
        Assert.True(options.PublishErrors);
        Assert.True(options.SubscriptionDispatchTotal);
        Assert.True(options.SubscriptionHandlerDuration);
    }

    [Fact]
    public void MetricsOptions_CanBeConfigured()
    {
        var options = new MetricsOptions
        {
            Enabled = false,
            MeterName = "custom",
            PublishDuration = false,
            PublishTotal = false,
            PublishErrors = false,
        };

        Assert.False(options.Enabled);
        Assert.Equal("custom", options.MeterName);
        Assert.False(options.PublishDuration);
        Assert.False(options.PublishTotal);
        Assert.False(options.PublishErrors);
    }

    [Fact]
    public void OpenTelemetryInstrumentationOptions_Metrics_HasCorrectDefaults()
    {
        var options = new OpenTelemetryInstrumentationOptions();

        Assert.NotNull(options.Metrics);
        Assert.True(options.Metrics.Enabled);
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

    private sealed class MetricsTestFixture : IAsyncDisposable
    {
        private readonly MeterListener _listener;
        private readonly Meter _meter;
        private readonly ServiceProvider _provider;
        private readonly string _meterName;

        public long PublishTotalCount { get; private set; }
        public long PublishErrorCount { get; private set; }
        public List<double> PublishDurationValues { get; } = new();
        public List<IReadOnlyList<KeyValuePair<string, object?>>> PublishDurationTags { get; } = new();
        public List<IReadOnlyList<KeyValuePair<string, object?>>> PublishTotalTags { get; } = new();

        public MetricsTestFixture(bool throwOnChannel = false, Action<OpenTelemetryInstrumentationOptions>? configure = null)
        {
            _meterName = $"Hermodr.Metrics.Test.{Guid.NewGuid():N}";
            _meter = new Meter(_meterName);
            TelemetryMetrics.Meter = _meter;

            var capturedActivities = new List<Activity>();
            var sourceName = $"Hermodr.Test.{Guid.NewGuid():N}";
            using var source = new ActivitySource(sourceName);
            var activityListener = new ActivityListener
            {
                ShouldListenTo = s => s.Name == sourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
                ActivityStarted = a => capturedActivities.Add(a),
            };
            ActivitySource.AddActivityListener(activityListener);

            _listener = new MeterListener();
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name != _meterName) return;
                if (instrument.Name == "hermodr.publish.total")
                {
                    listener.EnableMeasurementEvents(instrument);
                }
                else if (instrument.Name == "hermodr.publish.errors")
                {
                    listener.EnableMeasurementEvents(instrument);
                }
                else if (instrument.Name == "hermodr.publish.duration")
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };

            _listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
            {
                if (instrument.Name == "hermodr.publish.total")
                {
                    PublishTotalCount += measurement;
                    PublishTotalTags.Add(tags.ToArray());
                }
                else if (instrument.Name == "hermodr.publish.errors")
                {
                    PublishErrorCount += measurement;
                }
            });

            _listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) =>
            {
                if (instrument.Name == "hermodr.publish.duration")
                {
                    PublishDurationValues.Add(measurement);
                    PublishDurationTags.Add(tags.ToArray());
                }
            });

            _listener.Start();

            var services = new ServiceCollection().AddLogging();
            services.AddSingleton(source);
            services.AddSingleton(_meter);
            var builder = services.AddEventPublisher(opts =>
            {
                opts.Source = new Uri("https://example.com");
                opts.ThrowOnErrors = throwOnChannel;
            });
            builder.AddOpenTelemetryPublisherInstrumentation(o =>
            {
                o.ActivitySourceName = sourceName;
                o.Metrics.MeterName = _meterName;
                configure?.Invoke(o);
            });
            builder.AddTestChannel(e =>
            {
                if (throwOnChannel)
                    throw new InvalidOperationException("channel error");
            });
            _provider = services.BuildServiceProvider();
        }

        public EventPublisher CreatePublisher() => _provider.GetRequiredService<EventPublisher>();

        public ValueTask DisposeAsync()
        {
            _listener.Dispose();
            _meter.Dispose();
            return _provider.DisposeAsync();
        }
    }
}
