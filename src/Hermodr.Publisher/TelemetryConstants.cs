//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr
{
    /// <summary>
    /// Constants used by the OpenTelemetry instrumentation middleware.
    /// </summary>
    public static class TelemetryConstants
    {
        public const string DefaultActivitySourceName = "Hermodr";
        public const string DefaultMeterName = "Hermodr";

        public const string TraceParentExtensionName = "traceparent";
        public const string TraceStateExtensionName = "tracestate";

        public const string PublisherSpanNamePrefix = "publish";
        public const string ConsumerSpanNamePrefix = "handle";

        public const string ActivityItemKey = "Hermodr.Activity";

        public const string MessagingSystem = "hermodr";

        public const string MetricPublishDuration = "hermodr.publish.duration";
        public const string MetricPublishTotal = "hermodr.publish.total";
        public const string MetricPublishErrors = "hermodr.publish.errors";

        // Publisher domain
        public const string MetricEventEnrichDuration = "hermodr.event.enrich.duration";
        public const string MetricPipelineBypassTotal = "hermodr.pipeline.bypass_total";

        // Channel domain
        public const string MetricChannelPublishDuration = "hermodr.channel.publish.duration";

        // Dead letter domain
        public const string MetricReplayDuration = "hermodr.dead_letter.replay.duration";
        public const string MetricReplaySuccess = "hermodr.dead_letter.replay.success";
        public const string MetricReplayFailure = "hermodr.dead_letter.replay.failure";
        public const string MetricDeadLettersCreated = "hermodr.dead_letter.created";
        public const string MetricDeadLetterQueueDepth = "hermodr.dead_letter.queue_depth";
        public const string SpanDeadLetterReplay = "dead_letter.replay";
        public const string SpanDeadLetterStore = "dead_letter.store";

        // Audit trail domain
        public const string MetricAuditAppendDuration = "hermodr.audit.append.duration";
        public const string SpanAuditAppend = "audit.append";

        // Outbox domain
        public const string MetricOutboxStoreDuration = "hermodr.outbox.store.duration";
        public const string MetricOutboxMessagesAdded = "hermodr.outbox.messages.added";
        public const string MetricRelayDuration = "hermodr.outbox.relay.duration";
        public const string MetricRelayDelivered = "hermodr.outbox.relay.delivered";
        public const string MetricRelayFailed = "hermodr.outbox.relay.failed";
        public const string MetricRelayBatchSize = "hermodr.outbox.relay.batch_size";
        public const string MetricOutboxQueueDepth = "hermodr.outbox.queue_depth";
        public const string SpanOutboxStore = "outbox.store";
        public const string SpanOutboxRelay = "outbox.relay";

        // Pipeline diagnostics
        public const string MetricPipelineTotal = "hermodr.pipeline.total";

        // Subscription domain
        public const string MetricSubscriptionHandleDuration = "hermodr.subscription.handle.duration";
        public const string MetricSubscriptionMatched = "hermodr.subscription.matched";
        public const string MetricSubscriptionErrors = "hermodr.subscription.errors";

        // Transport spans
        public const string SpanTransportRabbitMq = "transport.publish.rabbitmq";
        public const string SpanTransportServiceBus = "transport.publish.servicebus";
        public const string SpanTransportMassTransit = "transport.publish.masstransit";
        public const string SpanTransportWebhook = "transport.publish.webhook";

        public const string PublishStartTimeItemKey = "Hermodr.Metrics.Publish.StartTime";

        public static string PublisherSpanName(string eventType) => $"{PublisherSpanNamePrefix} {eventType}";
        public static string ConsumerSpanName(string eventType) => $"{ConsumerSpanNamePrefix} {eventType}";
    }
}
