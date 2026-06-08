//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Diagnostics.Metrics;

using CloudNative.CloudEvents;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Hermodr;

[Trait("Category", "Unit")]
[Trait("Feature", "PublisherTelemetry")]
public class PublisherMetricsTests
{
    [Fact]
    public async Task PublishEventAsync_RecordsEventEnrichDuration()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var fixture = new PublisherMetricsTestFixture();
        var publisher = fixture.CreatePublisher();

        await publisher.PublishEventAsync(MakeEvent(), cancellationToken: cancellationToken);

        Assert.NotEmpty(fixture.EnrichDurationValues);
        Assert.All(fixture.EnrichDurationValues, v => Assert.True(v >= 0));
    }

    [Fact]
    public async Task PublishEventAsync_RecordsEnrichDurationWithEventTypeTag()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var fixture = new PublisherMetricsTestFixture();
        var publisher = fixture.CreatePublisher();

        await publisher.PublishEventAsync(MakeEvent("com.test.order.created"), cancellationToken: cancellationToken);

        Assert.Single(fixture.EnrichDurationTags);
        var tags = fixture.EnrichDurationTags[0];
        Assert.Contains(tags, kvp => kvp.Key == "event.type" && kvp.Value?.Equals("com.test.order.created") == true);
    }

    [Fact]
    public async Task PublishEventAsync_RecordsEnrichDurationForEachPublish()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var fixture = new PublisherMetricsTestFixture();
        var publisher = fixture.CreatePublisher();

        await publisher.PublishEventAsync(MakeEvent("com.test.first"), cancellationToken: cancellationToken);
        await publisher.PublishEventAsync(MakeEvent("com.test.second"), cancellationToken: cancellationToken);

        Assert.Equal(2, fixture.EnrichDurationValues.Count);
    }

    [Fact]
    public async Task PublishEventAsync_RecordsPipelineBypassTotal()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var fixture = new PublisherMetricsTestFixture();
        var publisher = fixture.CreatePublisher();

        await publisher.PublishEventAsync(MakeEvent(), EventPublishOptions.BypassPipeline(), cancellationToken: cancellationToken);

        Assert.Equal(1, fixture.PipelineBypassCount);
    }

    [Fact]
    public async Task PublishEventAsync_RecordsPipelineBypassWithEventTypeTag()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var fixture = new PublisherMetricsTestFixture();
        var publisher = fixture.CreatePublisher();

        await publisher.PublishEventAsync(MakeEvent("com.test.order"), EventPublishOptions.BypassPipeline(), cancellationToken: cancellationToken);

        Assert.Single(fixture.PipelineBypassTags);
        var tags = fixture.PipelineBypassTags[0];
        Assert.Contains(tags, kvp => kvp.Key == "event.type" && kvp.Value?.Equals("com.test.order") == true);
    }

    [Fact]
    public async Task PublishEventAsync_DoesNotRecordPipelineBypass_WhenNoBypass()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var fixture = new PublisherMetricsTestFixture();
        var publisher = fixture.CreatePublisher();

        await publisher.PublishEventAsync(MakeEvent(), cancellationToken: cancellationToken);

        Assert.Equal(0, fixture.PipelineBypassCount);
    }

    [Fact]
    public async Task PublishEventAsync_RecordsPipelineBypassForEachBypassedPublish()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var fixture = new PublisherMetricsTestFixture();
        var publisher = fixture.CreatePublisher();

        await publisher.PublishEventAsync(MakeEvent("com.test.first"), EventPublishOptions.BypassPipeline(), cancellationToken: cancellationToken);
        await publisher.PublishEventAsync(MakeEvent("com.test.second"), EventPublishOptions.BypassPipeline(), cancellationToken: cancellationToken);

        Assert.Equal(2, fixture.PipelineBypassCount);
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

    private sealed class PublisherMetricsTestFixture : IAsyncDisposable
    {
        private readonly Meter _testMeter;
        private readonly MeterListener _listener;
        private readonly ServiceProvider _provider;
        private readonly Meter _originalMeter;

        public List<double> EnrichDurationValues { get; } = new();
        public List<IReadOnlyList<KeyValuePair<string, object?>>> EnrichDurationTags { get; } = new();
        public long PipelineBypassCount { get; private set; }
        public List<IReadOnlyList<KeyValuePair<string, object?>>> PipelineBypassTags { get; } = new();

        public PublisherMetricsTestFixture()
        {
            _originalMeter = HermodrDiagnostics.Meter;
            _testMeter = new Meter($"Hermodr.Publisher.Test.{Guid.NewGuid():N}");
            HermodrDiagnostics.Meter = _testMeter;

            _listener = new MeterListener();
            _listener.InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name != _testMeter.Name) return;
                if (instrument.Name == TelemetryConstants.MetricEventEnrichDuration)
                    l.EnableMeasurementEvents(instrument);
                else if (instrument.Name == TelemetryConstants.MetricPipelineBypassTotal)
                    l.EnableMeasurementEvents(instrument);
            };

            _listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, _) =>
            {
                if (instrument.Name == TelemetryConstants.MetricEventEnrichDuration)
                {
                    EnrichDurationValues.Add(measurement);
                    EnrichDurationTags.Add(tags.ToArray());
                }
            });

            _listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
            {
                if (instrument.Name == TelemetryConstants.MetricPipelineBypassTotal)
                {
                    PipelineBypassCount += measurement;
                    PipelineBypassTags.Add(tags.ToArray());
                }
            });

            _listener.Start();

            var sourceName = $"Hermodr.Publisher.Test.{Guid.NewGuid():N}";
            using var source = new System.Diagnostics.ActivitySource(sourceName);
            var activityListener = new System.Diagnostics.ActivityListener
            {
                ShouldListenTo = s => s.Name == sourceName,
                Sample = (ref System.Diagnostics.ActivityCreationOptions<System.Diagnostics.ActivityContext> _) => System.Diagnostics.ActivitySamplingResult.AllData,
            };
            System.Diagnostics.ActivitySource.AddActivityListener(activityListener);

            var services = new ServiceCollection().AddLogging();
            services.AddSingleton(source);
            var builder = services.AddEventPublisher(opts => opts.Source = new Uri("https://example.com"));
            builder.UseOpenTelemetry(o =>
            {
                o.ActivitySourceName = sourceName;
                o.InstrumentSubscription = false;
                o.Metrics.MeterName = _testMeter.Name;
            });
            builder.AddTestChannel(_ => { });
            _provider = services.BuildServiceProvider();
        }

        public EventPublisher CreatePublisher() => _provider.GetRequiredService<EventPublisher>();

        public ValueTask DisposeAsync()
        {
            HermodrDiagnostics.Meter = _originalMeter;
            _testMeter.Dispose();
            _listener.Dispose();
            return _provider.DisposeAsync();
        }
    }
}
