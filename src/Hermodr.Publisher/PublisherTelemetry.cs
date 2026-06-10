using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Hermodr;

/// <summary>
/// Provides telemetry instrumentation for the event publisher pipeline.
/// </summary>
public sealed class PublisherTelemetry
{
    private readonly Histogram<double> _eventEnrichDuration;
    private readonly Counter<long> _pipelineBypassTotal;

    /// <summary>
    /// Initializes a new instance of the <see cref="PublisherTelemetry"/> class using the default meter.
    /// </summary>
    public PublisherTelemetry()
        : this(HermodrDiagnostics.Meter)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PublisherTelemetry"/> class with a specific meter.
    /// </summary>
    /// <param name="meter">The <see cref="Meter"/> to create instruments from.</param>
    public PublisherTelemetry(Meter meter)
    {
        _eventEnrichDuration = meter.CreateHistogram<double>(
            TelemetryConstants.MetricEventEnrichDuration, unit: "s",
            description: "Time to enrich an event before publishing");
        _pipelineBypassTotal = meter.CreateCounter<long>(
            TelemetryConstants.MetricPipelineBypassTotal, unit: "{event}",
            description: "Total number of pipeline bypass requests");
    }

    /// <summary>
    /// Records the duration of the event enrichment phase.
    /// </summary>
    /// <param name="seconds">The elapsed time in seconds.</param>
    /// <param name="eventType">The type of the event that was enriched.</param>
    public void RecordEventEnrichDuration(double seconds, string eventType)
    {
        _eventEnrichDuration.Record(seconds,
            new TagList { { TelemetryTags.EventType, eventType } });
    }

    /// <summary>
    /// Records a pipeline bypass event.
    /// </summary>
    /// <param name="eventType">The type of the event that bypassed the pipeline.</param>
    public void AddPipelineBypass(string eventType)
    {
        _pipelineBypassTotal.Add(1,
            new TagList { { TelemetryTags.EventType, eventType } });
    }
}
