using System.Diagnostics;

namespace Hermodr;

internal sealed class WebhookTransportTelemetry
{
    private readonly ActivitySource _activitySource;

    public WebhookTransportTelemetry()
        : this(HermodrDiagnostics.ActivitySource)
    {
    }

    public WebhookTransportTelemetry(ActivitySource activitySource)
    {
        _activitySource = activitySource;
    }

    public Activity? StartPublishActivity(string? eventType, string? endpointUrl)
    {
        return _activitySource.StartActivity(
            ActivityKind.Producer,
            name: TelemetryConstants.SpanTransportWebhook,
            tags: new ActivityTagsCollection
            {
                { TelemetryTags.EventType, eventType ?? "batch" },
                { TelemetryTags.MessagingSystem, "webhook" },
                { TelemetryTags.MessagingDestination, endpointUrl },
                { TelemetryTags.HttpRequestMethod, "POST" },
            });
    }
}
