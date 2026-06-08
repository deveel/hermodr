using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Hermodr;

public sealed class PublisherTelemetry
{
    private readonly Histogram<double> _eventEnrichDuration;
    private readonly Counter<long> _pipelineBypassTotal;

    public PublisherTelemetry()
        : this(HermodrDiagnostics.Meter)
    {
    }

    public PublisherTelemetry(Meter meter)
    {
        _eventEnrichDuration = meter.CreateHistogram<double>(
            TelemetryConstants.MetricEventEnrichDuration, unit: "s",
            description: "Time to enrich an event before publishing");
        _pipelineBypassTotal = meter.CreateCounter<long>(
            TelemetryConstants.MetricPipelineBypassTotal, unit: "{event}",
            description: "Total number of pipeline bypass requests");
    }

    public void RecordEventEnrichDuration(double seconds, string eventType)
    {
        _eventEnrichDuration.Record(seconds,
            new TagList { { TelemetryTags.EventType, eventType } });
    }

    public void AddPipelineBypass(string eventType)
    {
        _pipelineBypassTotal.Add(1,
            new TagList { { TelemetryTags.EventType, eventType } });
    }
}
