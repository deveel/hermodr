using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Hermodr;

internal sealed class DeadLetterTelemetry
{
    private readonly ActivitySource _activitySource;
    private readonly Histogram<double> _replayDuration;
    private readonly Counter<long> _replaySuccess;
    private readonly Counter<long> _replayFailure;
    private readonly Counter<long> _deadLettersCreated;
    private readonly Histogram<long> _deadLetterQueueDepth;

    public DeadLetterTelemetry()
        : this(HermodrDiagnostics.ActivitySource, HermodrDiagnostics.Meter)
    {
    }

    public DeadLetterTelemetry(ActivitySource activitySource, Meter meter)
    {
        _activitySource = activitySource;
        _replayDuration = meter.CreateHistogram<double>(
            TelemetryConstants.MetricReplayDuration, unit: "s",
            description: "Time to replay a dead-letter message");
        _replaySuccess = meter.CreateCounter<long>(
            TelemetryConstants.MetricReplaySuccess, unit: "{message}",
            description: "Number of successfully replayed dead-letter messages");
        _replayFailure = meter.CreateCounter<long>(
            TelemetryConstants.MetricReplayFailure, unit: "{message}",
            description: "Number of failed dead-letter message replays");
        _deadLettersCreated = meter.CreateCounter<long>(
            TelemetryConstants.MetricDeadLettersCreated, unit: "{message}",
            description: "Number of messages moved to the dead letter queue");
        _deadLetterQueueDepth = meter.CreateHistogram<long>(
            TelemetryConstants.MetricDeadLetterQueueDepth, unit: "{message}",
            description: "Number of pending messages in the dead letter queue");
    }

    public Activity? StartReplayActivity(string eventType, string messageId)
    {
        return _activitySource.StartActivity(
            ActivityKind.Internal,
            name: TelemetryConstants.SpanDeadLetterReplay,
            tags: new ActivityTagsCollection
            {
                { TelemetryTags.EventType, eventType },
                { TelemetryTags.MessageId, messageId },
            });
    }

    public Activity? StartStoreActivity(string eventType)
    {
        return _activitySource.StartActivity(
            ActivityKind.Internal,
            name: TelemetryConstants.SpanDeadLetterStore,
            tags: new ActivityTagsCollection
            {
                { TelemetryTags.EventType, eventType },
            });
    }

    public void RecordReplayDuration(double seconds, string eventType)
    {
        _replayDuration.Record(seconds,
            new TagList { { TelemetryTags.EventType, eventType } });
    }

    public void AddReplaySuccess(string eventType)
    {
        _replaySuccess.Add(1,
            new TagList { { TelemetryTags.EventType, eventType } });
    }

    public void AddReplayFailure(string eventType)
    {
        _replayFailure.Add(1,
            new TagList { { TelemetryTags.EventType, eventType } });
    }

    public void AddDeadLetterCreated(string eventType)
    {
        _deadLettersCreated.Add(1,
            new TagList { { TelemetryTags.EventType, eventType } });
    }

    public void RecordQueueDepth(long count)
    {
        _deadLetterQueueDepth.Record(count);
    }
}
