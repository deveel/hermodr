//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr;

[Trait("Category", "Unit")]
[Trait("Feature", "TelemetryConstants")]
public class TelemetryConstantsTests
{
    // ── Source / Meter names ─────────────────────────────────────────────────

    [Fact]
    public void DefaultActivitySourceName_IsHermodr()
    {
        Assert.Equal("Hermodr", TelemetryConstants.DefaultActivitySourceName);
    }

    [Fact]
    public void DefaultMeterName_IsHermodr()
    {
        Assert.Equal("Hermodr", TelemetryConstants.DefaultMeterName);
    }

    // ── Trace context extension names ────────────────────────────────────────

    [Fact]
    public void TraceParentExtensionName_IsCorrect()
    {
        Assert.Equal("traceparent", TelemetryConstants.TraceParentExtensionName);
    }

    [Fact]
    public void TraceStateExtensionName_IsCorrect()
    {
        Assert.Equal("tracestate", TelemetryConstants.TraceStateExtensionName);
    }

    // ── Span name prefixes ───────────────────────────────────────────────────

    [Fact]
    public void PublisherSpanNamePrefix_IsPublish()
    {
        Assert.Equal("publish", TelemetryConstants.PublisherSpanNamePrefix);
    }

    [Fact]
    public void ConsumerSpanNamePrefix_IsHandle()
    {
        Assert.Equal("handle", TelemetryConstants.ConsumerSpanNamePrefix);
    }

    [Fact]
    public void PublisherSpanName_FormatsCorrectly()
    {
        Assert.Equal("publish com.test.order", TelemetryConstants.PublisherSpanName("com.test.order"));
    }

    [Fact]
    public void ConsumerSpanName_FormatsCorrectly()
    {
        Assert.Equal("handle com.test.order", TelemetryConstants.ConsumerSpanName("com.test.order"));
    }

    // ── Activity item keys ───────────────────────────────────────────────────

    [Fact]
    public void ActivityItemKey_IsCorrect()
    {
        Assert.Equal("Hermodr.Activity", TelemetryConstants.ActivityItemKey);
    }

    [Fact]
    public void PublishStartTimeItemKey_IsCorrect()
    {
        Assert.Equal("Hermodr.Metrics.Publish.StartTime", TelemetryConstants.PublishStartTimeItemKey);
    }

    // ── Messaging system constant ────────────────────────────────────────────

    [Fact]
    public void MessagingSystem_IsHermodr()
    {
        Assert.Equal("hermodr", TelemetryConstants.MessagingSystem);
    }

    // ── Core publish metrics ─────────────────────────────────────────────────

    [Fact]
    public void MetricPublishDuration_IsCorrect()
    {
        Assert.Equal("hermodr.publish.duration", TelemetryConstants.MetricPublishDuration);
    }

    [Fact]
    public void MetricPublishTotal_IsCorrect()
    {
        Assert.Equal("hermodr.publish.total", TelemetryConstants.MetricPublishTotal);
    }

    [Fact]
    public void MetricPublishErrors_IsCorrect()
    {
        Assert.Equal("hermodr.publish.errors", TelemetryConstants.MetricPublishErrors);
    }

    // ── Publisher domain metrics ─────────────────────────────────────────────

    [Fact]
    public void MetricEventEnrichDuration_IsCorrect()
    {
        Assert.Equal("hermodr.event.enrich.duration", TelemetryConstants.MetricEventEnrichDuration);
    }

    [Fact]
    public void MetricPipelineBypassTotal_IsCorrect()
    {
        Assert.Equal("hermodr.pipeline.bypass_total", TelemetryConstants.MetricPipelineBypassTotal);
    }

    // ── Channel domain metrics ───────────────────────────────────────────────

    [Fact]
    public void MetricChannelPublishDuration_IsCorrect()
    {
        Assert.Equal("hermodr.channel.publish.duration", TelemetryConstants.MetricChannelPublishDuration);
    }

    // ── Dead letter domain metrics ───────────────────────────────────────────

    [Fact]
    public void MetricReplayDuration_IsCorrect()
    {
        Assert.Equal("hermodr.dead_letter.replay.duration", TelemetryConstants.MetricReplayDuration);
    }

    [Fact]
    public void MetricReplaySuccess_IsCorrect()
    {
        Assert.Equal("hermodr.dead_letter.replay.success", TelemetryConstants.MetricReplaySuccess);
    }

    [Fact]
    public void MetricReplayFailure_IsCorrect()
    {
        Assert.Equal("hermodr.dead_letter.replay.failure", TelemetryConstants.MetricReplayFailure);
    }

    [Fact]
    public void MetricDeadLettersCreated_IsCorrect()
    {
        Assert.Equal("hermodr.dead_letter.created", TelemetryConstants.MetricDeadLettersCreated);
    }

    [Fact]
    public void MetricDeadLetterQueueDepth_IsCorrect()
    {
        Assert.Equal("hermodr.dead_letter.queue_depth", TelemetryConstants.MetricDeadLetterQueueDepth);
    }

    [Fact]
    public void SpanDeadLetterReplay_IsCorrect()
    {
        Assert.Equal("dead_letter.replay", TelemetryConstants.SpanDeadLetterReplay);
    }

    [Fact]
    public void SpanDeadLetterStore_IsCorrect()
    {
        Assert.Equal("dead_letter.store", TelemetryConstants.SpanDeadLetterStore);
    }

    // ── Audit trail domain metrics ───────────────────────────────────────────

    [Fact]
    public void MetricAuditAppendDuration_IsCorrect()
    {
        Assert.Equal("hermodr.audit.append.duration", TelemetryConstants.MetricAuditAppendDuration);
    }

    [Fact]
    public void SpanAuditAppend_IsCorrect()
    {
        Assert.Equal("audit.append", TelemetryConstants.SpanAuditAppend);
    }

    // ── Outbox domain metrics ────────────────────────────────────────────────

    [Fact]
    public void MetricOutboxStoreDuration_IsCorrect()
    {
        Assert.Equal("hermodr.outbox.store.duration", TelemetryConstants.MetricOutboxStoreDuration);
    }

    [Fact]
    public void MetricOutboxMessagesAdded_IsCorrect()
    {
        Assert.Equal("hermodr.outbox.messages.added", TelemetryConstants.MetricOutboxMessagesAdded);
    }

    [Fact]
    public void MetricRelayDuration_IsCorrect()
    {
        Assert.Equal("hermodr.outbox.relay.duration", TelemetryConstants.MetricRelayDuration);
    }

    [Fact]
    public void MetricRelayDelivered_IsCorrect()
    {
        Assert.Equal("hermodr.outbox.relay.delivered", TelemetryConstants.MetricRelayDelivered);
    }

    [Fact]
    public void MetricRelayFailed_IsCorrect()
    {
        Assert.Equal("hermodr.outbox.relay.failed", TelemetryConstants.MetricRelayFailed);
    }

    [Fact]
    public void MetricRelayBatchSize_IsCorrect()
    {
        Assert.Equal("hermodr.outbox.relay.batch_size", TelemetryConstants.MetricRelayBatchSize);
    }

    [Fact]
    public void MetricOutboxQueueDepth_IsCorrect()
    {
        Assert.Equal("hermodr.outbox.queue_depth", TelemetryConstants.MetricOutboxQueueDepth);
    }

    [Fact]
    public void SpanOutboxStore_IsCorrect()
    {
        Assert.Equal("outbox.store", TelemetryConstants.SpanOutboxStore);
    }

    [Fact]
    public void SpanOutboxRelay_IsCorrect()
    {
        Assert.Equal("outbox.relay", TelemetryConstants.SpanOutboxRelay);
    }

    // ── Pipeline diagnostics metrics ─────────────────────────────────────────

    [Fact]
    public void MetricPipelineTotal_IsCorrect()
    {
        Assert.Equal("hermodr.pipeline.total", TelemetryConstants.MetricPipelineTotal);
    }

    // ── Subscription domain metrics ──────────────────────────────────────────

    [Fact]
    public void MetricSubscriptionHandleDuration_IsCorrect()
    {
        Assert.Equal("hermodr.subscription.handle.duration", TelemetryConstants.MetricSubscriptionHandleDuration);
    }

    [Fact]
    public void MetricSubscriptionMatched_IsCorrect()
    {
        Assert.Equal("hermodr.subscription.matched", TelemetryConstants.MetricSubscriptionMatched);
    }

    [Fact]
    public void MetricSubscriptionErrors_IsCorrect()
    {
        Assert.Equal("hermodr.subscription.errors", TelemetryConstants.MetricSubscriptionErrors);
    }

    // ── Transport span names ─────────────────────────────────────────────────

    [Fact]
    public void SpanTransportRabbitMq_IsCorrect()
    {
        Assert.Equal("transport.publish.rabbitmq", TelemetryConstants.SpanTransportRabbitMq);
    }

    [Fact]
    public void SpanTransportServiceBus_IsCorrect()
    {
        Assert.Equal("transport.publish.servicebus", TelemetryConstants.SpanTransportServiceBus);
    }

    [Fact]
    public void SpanTransportMassTransit_IsCorrect()
    {
        Assert.Equal("transport.publish.masstransit", TelemetryConstants.SpanTransportMassTransit);
    }

    [Fact]
    public void SpanTransportWebhook_IsCorrect()
    {
        Assert.Equal("transport.publish.webhook", TelemetryConstants.SpanTransportWebhook);
    }

    // ── Metric name format consistency ───────────────────────────────────────

    [Theory]
    [InlineData("hermodr.publish.duration")]
    [InlineData("hermodr.publish.total")]
    [InlineData("hermodr.publish.errors")]
    [InlineData("hermodr.event.enrich.duration")]
    [InlineData("hermodr.pipeline.bypass_total")]
    [InlineData("hermodr.channel.publish.duration")]
    [InlineData("hermodr.dead_letter.replay.duration")]
    [InlineData("hermodr.dead_letter.replay.success")]
    [InlineData("hermodr.dead_letter.replay.failure")]
    [InlineData("hermodr.dead_letter.created")]
    [InlineData("hermodr.dead_letter.queue_depth")]
    [InlineData("hermodr.audit.append.duration")]
    [InlineData("hermodr.outbox.store.duration")]
    [InlineData("hermodr.outbox.messages.added")]
    [InlineData("hermodr.outbox.relay.duration")]
    [InlineData("hermodr.outbox.relay.delivered")]
    [InlineData("hermodr.outbox.relay.failed")]
    [InlineData("hermodr.outbox.relay.batch_size")]
    [InlineData("hermodr.outbox.queue_depth")]
    [InlineData("hermodr.pipeline.total")]
    [InlineData("hermodr.subscription.handle.duration")]
    [InlineData("hermodr.subscription.matched")]
    [InlineData("hermodr.subscription.errors")]
    public void AllMetricNames_StartWithHermodrPrefix(string metricName)
    {
        Assert.StartsWith("hermodr.", metricName);
    }

    [Theory]
    [InlineData("dead_letter.replay")]
    [InlineData("dead_letter.store")]
    [InlineData("audit.append")]
    [InlineData("outbox.store")]
    [InlineData("outbox.relay")]
    [InlineData("transport.publish.rabbitmq")]
    [InlineData("transport.publish.servicebus")]
    [InlineData("transport.publish.masstransit")]
    [InlineData("transport.publish.webhook")]
    public void AllSpanNames_ContainDotSeparator(string spanName)
    {
        Assert.Contains('.', spanName);
    }
}
