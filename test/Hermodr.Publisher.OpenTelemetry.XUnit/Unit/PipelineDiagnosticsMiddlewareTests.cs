using System.Diagnostics;
using System.Diagnostics.Metrics;

using CloudNative.CloudEvents;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Hermodr;

[Trait("Category", "Unit")]
[Trait("Feature", "Diagnostics")]
public class PipelineDiagnosticsMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_RecordsPipelineTotalCounter()
    {
        var meterName = $"pipeline.test.{Guid.NewGuid():N}";
        var recordedValues = new List<long>();

        await using var fixture = new PipelineTestFixture(meterName, recordedValues);
        var cancellationToken = TestContext.Current.CancellationToken;
        var publisher = fixture.CreatePublisher();
        await publisher.PublishEventAsync(MakeEvent(), cancellationToken: cancellationToken);

        Assert.Equal(1, recordedValues.Sum());
    }

    [Fact]
    public async Task InvokeAsync_DoesNotRecord_WhenMetricsDisabled()
    {
        var meterName = $"pipeline.disabled.{Guid.NewGuid():N}";
        var recordedValues = new List<long>();

        await using var fixture = new PipelineTestFixture(meterName, recordedValues, opts => opts.Metrics.Enabled = false);
        var cancellationToken = TestContext.Current.CancellationToken;
        var publisher = fixture.CreatePublisher();
        await publisher.PublishEventAsync(MakeEvent(), cancellationToken: cancellationToken);

        Assert.Empty(recordedValues);
    }

    [Fact]
    public async Task InvokeAsync_RecordsAfterNextDelegateCompletes()
    {
        var meterName = $"pipeline.order.{Guid.NewGuid():N}";
        var recordedValues = new List<long>();

        await using var fixture = new PipelineTestFixture(meterName, recordedValues);
        var cancellationToken = TestContext.Current.CancellationToken;
        var publisher = fixture.CreatePublisher();
        await publisher.PublishEventAsync(MakeEvent(), cancellationToken: cancellationToken);

        Assert.Equal(1, recordedValues.Sum());
    }

    [Fact]
    public void Constructor_AcceptsNullLogger()
    {
        using var meter = new Meter("Hermodr");
        var options = Options.Create(new OpenTelemetryInstrumentationOptions());
        var middleware = new PipelineDiagnosticsMiddleware(meter, options, null);
        Assert.NotNull(middleware);
    }

    [Fact]
    public void Constructor_AcceptsNoOptions()
    {
        using var meter = new Meter("Hermodr");
        var middleware = new PipelineDiagnosticsMiddleware(meter);
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

    private sealed class PipelineTestFixture : IAsyncDisposable
    {
        private readonly MeterListener _meterListener;
        private readonly ActivityListener _activityListener;
        private readonly ActivitySource _source;
        private readonly ServiceProvider _provider;

        public PipelineTestFixture(
            string meterName,
            List<long> recordedValues,
            Action<OpenTelemetryInstrumentationOptions>? configure = null)
        {
            _meterListener = new MeterListener();
            _meterListener.InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == meterName && instrument.Name == "hermodr.pipeline.total")
                    l.EnableMeasurementEvents(instrument);
            };
            _meterListener.SetMeasurementEventCallback<long>((_, val, _, _) => recordedValues.Add(val));
            _meterListener.Start();

            var sourceName = $"Hermodr.Pipeline.Test.{Guid.NewGuid():N}";
            _source = new ActivitySource(sourceName);
            _activityListener = new ActivityListener
            {
                ShouldListenTo = s => s.Name == sourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            };
            ActivitySource.AddActivityListener(_activityListener);

            var services = new ServiceCollection().AddLogging();
            services.AddSingleton(_source);
            var builder = services.AddEventPublisher(opts => opts.Source = new Uri("https://example.com"));
            builder.UseOpenTelemetry(o =>
            {
                o.ActivitySourceName = sourceName;
                o.InstrumentPublisher = false;
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
