using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Hermodr;

public sealed class ChannelTelemetry
{
    private readonly Histogram<double> _channelPublishDuration;

    public ChannelTelemetry()
        : this(HermodrDiagnostics.Meter)
    {
    }

    public ChannelTelemetry(Meter meter)
    {
        _channelPublishDuration = meter.CreateHistogram<double>(
            TelemetryConstants.MetricChannelPublishDuration, unit: "s",
            description: "Time to publish an event through a specific channel");
    }

    public void RecordChannelPublishDuration(double seconds, string eventType, string channelType)
    {
        _channelPublishDuration.Record(seconds,
            new TagList
            {
                { TelemetryTags.EventType, eventType },
                { TelemetryTags.ChannelType, channelType }
            });
    }
}
