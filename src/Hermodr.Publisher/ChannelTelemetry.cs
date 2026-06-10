using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Hermodr;

/// <summary>
/// Provides telemetry instrumentation for event publish channel operations.
/// </summary>
public sealed class ChannelTelemetry
{
    private readonly Histogram<double> _channelPublishDuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChannelTelemetry"/> class using the default meter.
    /// </summary>
    public ChannelTelemetry()
        : this(HermodrDiagnostics.Meter)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ChannelTelemetry"/> class with a specific meter.
    /// </summary>
    /// <param name="meter">The <see cref="Meter"/> to create instruments from.</param>
    public ChannelTelemetry(Meter meter)
    {
        _channelPublishDuration = meter.CreateHistogram<double>(
            TelemetryConstants.MetricChannelPublishDuration, unit: "s",
            description: "Time to publish an event through a specific channel");
    }

    /// <summary>
    /// Records the duration of a channel publish operation.
    /// </summary>
    /// <param name="seconds">The elapsed time in seconds.</param>
    /// <param name="eventType">The type of the published event.</param>
    /// <param name="channelType">The type name of the publishing channel.</param>
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
