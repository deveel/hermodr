using System.Diagnostics;

namespace Hermodr;

internal sealed class RabbitMqTransportTelemetry
{
    private readonly ActivitySource _activitySource;

    public RabbitMqTransportTelemetry()
        : this(HermodrDiagnostics.ActivitySource)
    {
    }

    public RabbitMqTransportTelemetry(ActivitySource activitySource)
    {
        _activitySource = activitySource;
    }

    public Activity? StartPublishActivity(string eventType, string exchangeName, string routingKey)
    {
        return _activitySource.StartActivity(
            ActivityKind.Producer,
            name: TelemetryConstants.SpanTransportRabbitMq,
            tags: new ActivityTagsCollection
            {
                { TelemetryTags.EventType, eventType },
                { TelemetryTags.MessagingSystem, "rabbitmq" },
                { TelemetryTags.MessagingDestination, exchangeName },
                { TelemetryTags.MessagingDestinationKind, "exchange" },
                { TelemetryTags.MessagingRabbitMqRoutingKey, routingKey },
            });
    }
}
