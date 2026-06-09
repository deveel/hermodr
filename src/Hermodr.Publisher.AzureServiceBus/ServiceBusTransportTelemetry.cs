using System.Diagnostics;

namespace Hermodr;

internal sealed class ServiceBusTransportTelemetry
{
    private readonly ActivitySource _activitySource;

    public ServiceBusTransportTelemetry()
        : this(HermodrDiagnostics.ActivitySource)
    {
    }

    public ServiceBusTransportTelemetry(ActivitySource activitySource)
    {
        _activitySource = activitySource;
    }

    public Activity? StartPublishActivity(string eventType, string queueName)
    {
        return _activitySource.StartActivity(
            ActivityKind.Producer,
            name: TelemetryConstants.SpanTransportServiceBus,
            tags: new ActivityTagsCollection
            {
                { TelemetryTags.EventType, eventType },
                { TelemetryTags.MessagingSystem, "azure_servicebus" },
                { TelemetryTags.MessagingDestination, queueName },
                { TelemetryTags.MessagingDestinationKind, "queue" },
            });
    }
}
