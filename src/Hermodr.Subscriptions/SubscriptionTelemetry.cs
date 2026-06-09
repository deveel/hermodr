using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Hermodr;

internal sealed class SubscriptionTelemetry
{
    private readonly Histogram<double> _handleDuration;
    private readonly Histogram<long> _matched;
    private readonly Counter<long> _errors;

    public SubscriptionTelemetry()
        : this(HermodrDiagnostics.Meter)
    {
    }

    public SubscriptionTelemetry(Meter meter)
    {
        _handleDuration = meter.CreateHistogram<double>(
            TelemetryConstants.MetricSubscriptionHandleDuration, unit: "s",
            description: "Time per subscription handler invocation");
        _matched = meter.CreateHistogram<long>(
            TelemetryConstants.MetricSubscriptionMatched, unit: "{subscription}",
            description: "Number of matching subscriptions per event");
        _errors = meter.CreateCounter<long>(
            TelemetryConstants.MetricSubscriptionErrors, unit: "{error}",
            description: "Number of subscription handler errors");
    }

    public void RecordHandleDuration(double seconds, string eventType, string subscriptionName)
    {
        _handleDuration.Record(seconds,
            new TagList
            {
                { TelemetryTags.EventType, eventType },
                { TelemetryTags.SubscriptionName, subscriptionName }
            });
    }

    public void RecordMatched(long count, string eventType)
    {
        _matched.Record(count,
            new TagList { { TelemetryTags.EventType, eventType } });
    }

    public void AddError(string eventType, string subscriptionName)
    {
        _errors.Add(1,
            new TagList
            {
                { TelemetryTags.EventType, eventType },
                { TelemetryTags.SubscriptionName, subscriptionName }
            });
    }
}
