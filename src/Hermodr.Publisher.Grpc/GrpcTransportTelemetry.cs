using System.Diagnostics;

namespace Hermodr;

internal sealed class GrpcTransportTelemetry
{
    private readonly ActivitySource _activitySource;

    public GrpcTransportTelemetry()
        : this(HermodrDiagnostics.ActivitySource)
    {
    }

    public GrpcTransportTelemetry(ActivitySource activitySource)
    {
        _activitySource = activitySource;
    }

    public Activity? StartPublishActivity(string? eventType, string? address, string rpcType = "unary")
    {
        return _activitySource.StartActivity(
            ActivityKind.Producer,
            name: TelemetryConstants.SpanTransportGrpc,
            tags: new ActivityTagsCollection
            {
                { TelemetryTags.EventType, eventType ?? "unknown" },
                { TelemetryTags.MessagingSystem, "grpc" },
                { TelemetryTags.MessagingDestination, address },
                { "rpc.type", rpcType },
            });
    }
}
