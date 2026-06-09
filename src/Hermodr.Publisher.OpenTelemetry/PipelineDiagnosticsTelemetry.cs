using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Hermodr;

internal sealed class PipelineDiagnosticsTelemetry
{
    private readonly Counter<long>? _pipelineTotal;

    public PipelineDiagnosticsTelemetry(Meter meter, bool enabled = true)
    {
        if (enabled)
        {
            _pipelineTotal = meter.CreateCounter<long>(
                TelemetryConstants.MetricPipelineTotal, unit: "{event}",
                description: "Total number of events flowing through the pipeline");
        }
    }

    public bool IsEnabled => _pipelineTotal != null;

    public void AddPipelineTotal(string eventType)
    {
        _pipelineTotal?.Add(1, new TagList { { TelemetryTags.EventType, eventType } });
    }
}
