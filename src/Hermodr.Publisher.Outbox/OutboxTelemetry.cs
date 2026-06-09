using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Hermodr;

internal sealed class OutboxTelemetry
{
    private readonly ActivitySource _activitySource;
    private readonly Histogram<double> _outboxStoreDuration;
    private readonly Counter<long> _outboxMessagesAdded;
    private readonly Histogram<double> _relayDuration;
    private readonly Counter<long> _relayDelivered;
    private readonly Counter<long> _relayFailed;
    private readonly Histogram<long> _relayBatchSize;
    private readonly Histogram<long> _outboxQueueDepth;

    public OutboxTelemetry()
        : this(HermodrDiagnostics.ActivitySource, HermodrDiagnostics.Meter)
    {
    }

    public OutboxTelemetry(ActivitySource activitySource, Meter meter)
    {
        _activitySource = activitySource;
        _outboxStoreDuration = meter.CreateHistogram<double>(
            TelemetryConstants.MetricOutboxStoreDuration, unit: "s",
            description: "Time to store an event in the outbox");
        _outboxMessagesAdded = meter.CreateCounter<long>(
            TelemetryConstants.MetricOutboxMessagesAdded, unit: "{message}",
            description: "Number of messages added to the outbox");
        _relayDuration = meter.CreateHistogram<double>(
            TelemetryConstants.MetricRelayDuration, unit: "s",
            description: "Time to relay a single outbox message to transport");
        _relayDelivered = meter.CreateCounter<long>(
            TelemetryConstants.MetricRelayDelivered, unit: "{message}",
            description: "Number of outbox messages successfully delivered to transport");
        _relayFailed = meter.CreateCounter<long>(
            TelemetryConstants.MetricRelayFailed, unit: "{message}",
            description: "Number of outbox messages that failed to deliver");
        _relayBatchSize = meter.CreateHistogram<long>(
            TelemetryConstants.MetricRelayBatchSize, unit: "{message}",
            description: "Batch size of outbox relay processing");
        _outboxQueueDepth = meter.CreateHistogram<long>(
            TelemetryConstants.MetricOutboxQueueDepth, unit: "{message}",
            description: "Number of pending messages in the outbox");
    }

    public Activity? StartStoreActivity(string eventType)
    {
        return _activitySource.StartActivity(
            ActivityKind.Producer,
            name: TelemetryConstants.SpanOutboxStore,
            tags: new ActivityTagsCollection
            {
                { TelemetryTags.EventType, eventType },
            });
    }

    public Activity? StartRelayActivity(string eventType)
    {
        return _activitySource.StartActivity(
            ActivityKind.Producer,
            name: TelemetryConstants.SpanOutboxRelay,
            tags: new ActivityTagsCollection
            {
                { TelemetryTags.EventType, eventType },
            });
    }

    public void RecordStoreDuration(double seconds, string eventType)
    {
        _outboxStoreDuration.Record(seconds,
            new TagList { { TelemetryTags.EventType, eventType } });
    }

    public void AddMessageAdded(string eventType)
    {
        _outboxMessagesAdded.Add(1,
            new TagList { { TelemetryTags.EventType, eventType } });
    }

    public void RecordRelayDuration(double seconds, string eventType)
    {
        _relayDuration.Record(seconds,
            new TagList { { TelemetryTags.EventType, eventType } });
    }

    public void AddRelayDelivered(string eventType)
    {
        _relayDelivered.Add(1,
            new TagList { { TelemetryTags.EventType, eventType } });
    }

    public void AddRelayFailed(string eventType)
    {
        _relayFailed.Add(1,
            new TagList { { TelemetryTags.EventType, eventType } });
    }

    public void RecordRelayBatchSize(long count)
    {
        _relayBatchSize.Record(count);
    }

    public void RecordQueueDepth(long count)
    {
        _outboxQueueDepth.Record(count);
    }
}
