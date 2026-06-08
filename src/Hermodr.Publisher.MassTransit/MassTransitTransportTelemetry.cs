using System.Diagnostics;

namespace Hermodr;

internal sealed class MassTransitTransportTelemetry
{
    private readonly ActivitySource _activitySource;

    public MassTransitTransportTelemetry()
        : this(HermodrDiagnostics.ActivitySource)
    {
    }

    public MassTransitTransportTelemetry(ActivitySource activitySource)
    {
        _activitySource = activitySource;
    }

    public Activity? StartPublishActivity(string eventType)
    {
        return _activitySource.StartActivity(
            ActivityKind.Producer,
            name: TelemetryConstants.SpanTransportMassTransit,
            tags: new ActivityTagsCollection
            {
                { TelemetryTags.EventType, eventType },
                { TelemetryTags.MessagingSystem, "masstransit" },
            });
    }
}
