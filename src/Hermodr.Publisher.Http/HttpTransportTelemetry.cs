using System.Diagnostics;

namespace Hermodr;

internal sealed class HttpTransportTelemetry
{
    private readonly ActivitySource _activitySource;

    public HttpTransportTelemetry()
        : this(HermodrDiagnostics.ActivitySource)
    {
    }

    public HttpTransportTelemetry(ActivitySource activitySource)
    {
        _activitySource = activitySource;
    }

    public Activity? StartPublishActivity(string? eventType, string? endpointUrl)
    {
        return _activitySource.StartActivity(
            ActivityKind.Producer,
            name: TelemetryConstants.SpanTransportHttp,
            tags: new ActivityTagsCollection
            {
                { TelemetryTags.EventType, eventType ?? "unknown" },
                { TelemetryTags.MessagingSystem, "http" },
                { TelemetryTags.MessagingDestination, endpointUrl },
                { TelemetryTags.HttpRequestMethod, "POST" },
            });
    }
}