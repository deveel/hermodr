//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Diagnostics.Metrics;

using CloudNative.CloudEvents;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hermodr;

[Trait("Category", "Unit")]
[Trait("Feature", "PipelineDiagnosticsTelemetry")]
public class PipelineDiagnosticsTelemetryTests
{
    [Fact]
    public void Middleware_Constructor_UsesNullLogger()
    {
        using var meter = new Meter("Hermodr");
        var options = Options.Create(new OpenTelemetryInstrumentationOptions());
        var middleware = new PipelineDiagnosticsMiddleware(meter, options, null);
        Assert.NotNull(middleware);
    }

    [Fact]
    public void Middleware_Constructor_UsesDefaultOptions()
    {
        using var meter = new Meter("Hermodr");
        var middleware = new PipelineDiagnosticsMiddleware(meter);
        Assert.NotNull(middleware);
    }

    [Fact]
    public async Task Middleware_RecordsPipelineTotal()
    {
        var meterName = $"pipeline.telemetry.test.{Guid.NewGuid():N}";
        var recordedValues = new List<long>();

        await using var fixture = new PipelineTelemetryTestFixture(meterName, recordedValues);
        var cancellationToken = TestContext.Current.CancellationToken;
        var publisher = fixture.CreatePublisher();
        await publisher.PublishEventAsync(MakeEvent(), cancellationToken: cancellationToken);

        Assert.Equal(1, recordedValues.Sum());
    }

    [Fact]
    public async Task Middleware_DoesNotRecord_WhenMetricsDisabled()
    {
        var meterName = $"pipeline.telemetry.disabled.{Guid.NewGuid():N}";
        var recordedValues = new List<long>();

        await using var fixture = new PipelineTelemetryTestFixture(meterName, recordedValues, opts => opts.Metrics.Enabled = false);
        var cancellationToken = TestContext.Current.CancellationToken;
        var publisher = fixture.CreatePublisher();
        await publisher.PublishEventAsync(MakeEvent(), cancellationToken: cancellationToken);

        Assert.Empty(recordedValues);
    }

    [Fact]
    public async Task Middleware_RecordsEventTypeTag_OnPipelineTotal()
    {
        var meterName = $"pipeline.telemetry.tags.{Guid.NewGuid():N}";
        var recordedTags = new List<IReadOnlyList<KeyValuePair<string, object?>>>();

        await using var fixture = new PipelineTelemetryTestFixture(meterName, recordedTags);
        var cancellationToken = TestContext.Current.CancellationToken;
        var publisher = fixture.CreatePublisher();
        await publisher.PublishEventAsync(MakeEvent("com.test.order"), cancellationToken: cancellationToken);

        Assert.Single(recordedTags);
        Assert.Contains(recordedTags[0], kvp => kvp.Key == "event.type" && kvp.Value?.Equals("com.test.order") == true);
    }

    [Fact]
    public async Task Middleware_RecordsOncePerPublish_RegardlessOfChannelCount()
    {
        var meterName = $"pipeline.telemetry.once.{Guid.NewGuid():N}";
        var recordedValues = new List<long>();

        await using var fixture = new PipelineTelemetryTestFixture(meterName, recordedValues, channelCount: 3);
        var cancellationToken = TestContext.Current.CancellationToken;
        var publisher = fixture.CreatePublisher();
        await publisher.PublishEventAsync(MakeEvent(), cancellationToken: cancellationToken);

        Assert.Equal(1, recordedValues.Sum());
    }

    [Fact]
    public async Task Middleware_RecordsForEachPublish()
    {
        var meterName = $"pipeline.telemetry.each.{Guid.NewGuid():N}";
        var recordedValues = new List<long>();

        await using var fixture = new PipelineTelemetryTestFixture(meterName, recordedValues);
        var cancellationToken = TestContext.Current.CancellationToken;
        var publisher = fixture.CreatePublisher();
        await publisher.PublishEventAsync(MakeEvent("com.test.first"), cancellationToken: cancellationToken);
        await publisher.PublishEventAsync(MakeEvent("com.test.second"), cancellationToken: cancellationToken);
        await publisher.PublishEventAsync(MakeEvent("com.test.third"), cancellationToken: cancellationToken);

        Assert.Equal(3, recordedValues.Sum());
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

    private sealed class PipelineTelemetryTestFixture : IAsyncDisposable
    {
        private readonly MeterListener _meterListener;
        private readonly System.Diagnostics.ActivityListener _activityListener;
        private readonly System.Diagnostics.ActivitySource _source;
        private readonly ServiceProvider _provider;

        public PipelineTelemetryTestFixture(
            string meterName,
            List<long> recordedValues,
            Action<OpenTelemetryInstrumentationOptions>? configure = null,
            int channelCount = 1)
        {
            _meterListener = new MeterListener();
            _meterListener.InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == meterName && instrument.Name == TelemetryConstants.MetricPipelineTotal)
                    l.EnableMeasurementEvents(instrument);
            };
            _meterListener.SetMeasurementEventCallback<long>((_, val, _, _) => recordedValues.Add(val));
            _meterListener.Start();

            var sourceName = $"Hermodr.PipelineTelemetry.Test.{Guid.NewGuid():N}";
            _source = new System.Diagnostics.ActivitySource(sourceName);
            _activityListener = new System.Diagnostics.ActivityListener
            {
                ShouldListenTo = s => s.Name == sourceName,
                Sample = (ref System.Diagnostics.ActivityCreationOptions<System.Diagnostics.ActivityContext> _) => System.Diagnostics.ActivitySamplingResult.AllData,
            };
            System.Diagnostics.ActivitySource.AddActivityListener(_activityListener);

            var services = new ServiceCollection().AddLogging();
            services.AddSingleton(_source);
            var builder = services.AddEventPublisher(opts => opts.Source = new Uri("https://example.com"));
            builder.UseOpenTelemetry(o =>
            {
                o.ActivitySourceName = sourceName;
                o.InstrumentSubscription = false;
                o.Metrics.MeterName = meterName;
                configure?.Invoke(o);
            });

            for (var i = 0; i < channelCount; i++)
            {
                builder.AddTestChannel(_ => { });
            }

            _provider = services.BuildServiceProvider();
        }

        public PipelineTelemetryTestFixture(
            string meterName,
            List<IReadOnlyList<KeyValuePair<string, object?>>> recordedTags,
            Action<OpenTelemetryInstrumentationOptions>? configure = null)
        {
            _meterListener = new MeterListener();
            _meterListener.InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == meterName && instrument.Name == TelemetryConstants.MetricPipelineTotal)
                    l.EnableMeasurementEvents(instrument);
            };
            _meterListener.SetMeasurementEventCallback<long>((_, _, tags, _) => recordedTags.Add(tags.ToArray()));
            _meterListener.Start();

            var sourceName = $"Hermodr.PipelineTelemetry.Test.{Guid.NewGuid():N}";
            _source = new System.Diagnostics.ActivitySource(sourceName);
            _activityListener = new System.Diagnostics.ActivityListener
            {
                ShouldListenTo = s => s.Name == sourceName,
                Sample = (ref System.Diagnostics.ActivityCreationOptions<System.Diagnostics.ActivityContext> _) => System.Diagnostics.ActivitySamplingResult.AllData,
            };
            System.Diagnostics.ActivitySource.AddActivityListener(_activityListener);

            var services = new ServiceCollection().AddLogging();
            services.AddSingleton(_source);
            var builder = services.AddEventPublisher(opts => opts.Source = new Uri("https://example.com"));
            builder.UseOpenTelemetry(o =>
            {
                o.ActivitySourceName = sourceName;
                o.InstrumentSubscription = false;
                o.Metrics.MeterName = meterName;
                configure?.Invoke(o);
            });
            builder.AddTestChannel(_ => { });
            _provider = services.BuildServiceProvider();
        }

        public EventPublisher CreatePublisher() => _provider.GetRequiredService<EventPublisher>();

        public ValueTask DisposeAsync()
        {
            _meterListener.Dispose();
            _activityListener.Dispose();
            _source.Dispose();
            return _provider.DisposeAsync();
        }
    }
}
