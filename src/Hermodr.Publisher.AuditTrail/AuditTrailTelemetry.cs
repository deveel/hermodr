using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Hermodr;

internal sealed class AuditTrailTelemetry
{
    private readonly ActivitySource _activitySource;
    private readonly Histogram<double> _auditAppendDuration;

    public AuditTrailTelemetry()
        : this(HermodrDiagnostics.ActivitySource, HermodrDiagnostics.Meter)
    {
    }

    public AuditTrailTelemetry(ActivitySource activitySource, Meter meter)
    {
        _activitySource = activitySource;
        _auditAppendDuration = meter.CreateHistogram<double>(
            TelemetryConstants.MetricAuditAppendDuration, unit: "s",
            description: "Time to append an event to the audit trail");
    }

    public Activity? StartAppendActivity(string eventType)
    {
        return _activitySource.StartActivity(
            ActivityKind.Producer,
            name: TelemetryConstants.SpanAuditAppend,
            tags: new ActivityTagsCollection
            {
                { TelemetryTags.EventType, eventType },
            });
    }

    public void RecordAppendDuration(double seconds, string eventType)
    {
        _auditAppendDuration.Record(seconds,
            new TagList { { TelemetryTags.EventType, eventType } });
    }
}
