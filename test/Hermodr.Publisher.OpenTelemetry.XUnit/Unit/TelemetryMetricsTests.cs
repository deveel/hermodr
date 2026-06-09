using System.Diagnostics.Metrics;

namespace Hermodr;

[Trait("Category", "Unit")]
[Trait("Feature", "Diagnostics")]
public class TelemetryMetricsTests
{
    [Fact]
    public void Meter_DelegatesToHermodrDiagnostics()
    {
        Assert.Same(HermodrDiagnostics.Meter, TelemetryMetrics.Meter);
    }

    [Fact]
    public void CreatePublishDurationHistogram_CreatesWithCorrectName()
    {
        using var meter = new Meter("test-meter");
        var histogram = TelemetryMetrics.CreatePublishDurationHistogram(meter);
        Assert.Equal("hermodr.publish.duration", histogram.Name);
    }

    [Fact]
    public void CreatePublishDurationHistogram_CreatesWithSecondUnit()
    {
        using var meter = new Meter("test-meter");
        var histogram = TelemetryMetrics.CreatePublishDurationHistogram(meter);
        Assert.Equal("s", histogram.Unit);
    }

    [Fact]
    public void CreatePublishTotalCounter_CreatesWithCorrectName()
    {
        using var meter = new Meter("test-meter");
        var counter = TelemetryMetrics.CreatePublishTotalCounter(meter);
        Assert.Equal("hermodr.publish.total", counter.Name);
    }

    [Fact]
    public void CreatePublishTotalCounter_CreatesWithEventUnit()
    {
        using var meter = new Meter("test-meter");
        var counter = TelemetryMetrics.CreatePublishTotalCounter(meter);
        Assert.Equal("{event}", counter.Unit);
    }

    [Fact]
    public void CreatePublishErrorsCounter_CreatesWithCorrectName()
    {
        using var meter = new Meter("test-meter");
        var counter = TelemetryMetrics.CreatePublishErrorsCounter(meter);
        Assert.Equal("hermodr.publish.errors", counter.Name);
    }

    [Fact]
    public void CreatePublishErrorsCounter_CreatesWithErrorUnit()
    {
        using var meter = new Meter("test-meter");
        var counter = TelemetryMetrics.CreatePublishErrorsCounter(meter);
        Assert.Equal("{error}", counter.Unit);
    }

    [Fact]
    public void CreatePublishDurationHistogram_RecordsValues()
    {
        using var meter = new Meter("test-histogram");
        var histogram = TelemetryMetrics.CreatePublishDurationHistogram(meter);
        var recordedValues = new List<double>();

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Name == "hermodr.publish.duration")
                l.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<double>((_, val, _, _) => recordedValues.Add(val));
        listener.Start();

        histogram.Record(1.5);

        Assert.Contains(1.5, recordedValues);
    }

    [Fact]
    public void CreatePublishTotalCounter_RecordsValues()
    {
        using var meter = new Meter("test-total");
        var counter = TelemetryMetrics.CreatePublishTotalCounter(meter);
        var recordedValues = new List<long>();

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Name == "hermodr.publish.total")
                l.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((_, val, _, _) => recordedValues.Add(val));
        listener.Start();

        counter.Add(3);

        Assert.Contains(3, recordedValues);
    }

    [Fact]
    public void CreatePublishErrorsCounter_RecordsValues()
    {
        using var meter = new Meter("test-errors");
        var counter = TelemetryMetrics.CreatePublishErrorsCounter(meter);
        var recordedValues = new List<long>();

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Name == "hermodr.publish.errors")
                l.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((_, val, _, _) => recordedValues.Add(val));
        listener.Start();

        counter.Add(2);

        Assert.Contains(2, recordedValues);
    }
}
