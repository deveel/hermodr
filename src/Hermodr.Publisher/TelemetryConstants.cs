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
        /// <summary>Default name of the OpenTelemetry activity source.</summary>
        public const string DefaultActivitySourceName = "Hermodr";
        /// <summary>Default name of the OpenTelemetry meter.</summary>
        public const string DefaultMeterName = "Hermodr";

        /// <summary>Name of the W3C trace context parent extension header.</summary>
        public const string TraceParentExtensionName = "traceparent";
        /// <summary>Name of the W3C trace state extension header.</summary>
        public const string TraceStateExtensionName = "tracestate";

        /// <summary>Prefix used to build publisher span names.</summary>
        public const string PublisherSpanNamePrefix = "publish";
        /// <summary>Prefix used to build consumer span names.</summary>
        public const string ConsumerSpanNamePrefix = "handle";

        /// <summary>Item key used to stash the current activity in the request items collection.</summary>
        public const string ActivityItemKey = "Hermodr.Activity";

        /// <summary>Value of the messaging.system tag identifying Hermodr as the messaging system.</summary>
        public const string MessagingSystem = "hermodr";

        /// <summary>Metric name for the overall publish operation duration.</summary>
        public const string MetricPublishDuration = "hermodr.publish.duration";
        /// <summary>Metric name for the total number of published messages.</summary>
        public const string MetricPublishTotal = "hermodr.publish.total";
        /// <summary>Metric name for the number of publish operation errors.</summary>
        public const string MetricPublishErrors = "hermodr.publish.errors";

        // Publisher domain
        /// <summary>Metric name for the event enrichment duration.</summary>
        public const string MetricEventEnrichDuration = "hermodr.event.enrich.duration";
        /// <summary>Metric name for the total number of messages that bypassed the pipeline.</summary>
        public const string MetricPipelineBypassTotal = "hermodr.pipeline.bypass_total";

        // Channel domain
        /// <summary>Metric name for the channel publish operation duration.</summary>
        public const string MetricChannelPublishDuration = "hermodr.channel.publish.duration";

        // Dead letter domain
        /// <summary>Metric name for the dead letter replay operation duration.</summary>
        public const string MetricReplayDuration = "hermodr.dead_letter.replay.duration";
        /// <summary>Metric name for the count of successful dead letter replays.</summary>
        public const string MetricReplaySuccess = "hermodr.dead_letter.replay.success";
        /// <summary>Metric name for the count of failed dead letter replays.</summary>
        public const string MetricReplayFailure = "hermodr.dead_letter.replay.failure";
        /// <summary>Metric name for the number of dead letters created.</summary>
        public const string MetricDeadLettersCreated = "hermodr.dead_letter.created";
        /// <summary>Metric name for the current dead letter queue depth.</summary>
        public const string MetricDeadLetterQueueDepth = "hermodr.dead_letter.queue_depth";
        /// <summary>Span name for the dead letter replay operation.</summary>
        public const string SpanDeadLetterReplay = "dead_letter.replay";
        /// <summary>Span name for the dead letter store operation.</summary>
        public const string SpanDeadLetterStore = "dead_letter.store";

        // Audit trail domain
        /// <summary>Metric name for the audit append operation duration.</summary>
        public const string MetricAuditAppendDuration = "hermodr.audit.append.duration";
        /// <summary>Span name for the audit append operation.</summary>
        public const string SpanAuditAppend = "audit.append";

        // Outbox domain
        /// <summary>Metric name for the outbox store operation duration.</summary>
        public const string MetricOutboxStoreDuration = "hermodr.outbox.store.duration";
        /// <summary>Metric name for the count of messages added to the outbox.</summary>
        public const string MetricOutboxMessagesAdded = "hermodr.outbox.messages.added";
        /// <summary>Metric name for the outbox relay duration.</summary>
        public const string MetricRelayDuration = "hermodr.outbox.relay.duration";
        /// <summary>Metric name for the count of messages delivered by the outbox relay.</summary>
        public const string MetricRelayDelivered = "hermodr.outbox.relay.delivered";
        /// <summary>Metric name for the count of messages the outbox relay failed to deliver.</summary>
        public const string MetricRelayFailed = "hermodr.outbox.relay.failed";
        /// <summary>Metric name for the outbox relay batch size.</summary>
        public const string MetricRelayBatchSize = "hermodr.outbox.relay.batch_size";
        /// <summary>Metric name for the current outbox queue depth.</summary>
        public const string MetricOutboxQueueDepth = "hermodr.outbox.queue_depth";
        /// <summary>Span name for the outbox store operation.</summary>
        public const string SpanOutboxStore = "outbox.store";
        /// <summary>Span name for the outbox relay operation.</summary>
        public const string SpanOutboxRelay = "outbox.relay";

        // Pipeline diagnostics
        /// <summary>Metric name for the total number of messages processed by the pipeline.</summary>
        public const string MetricPipelineTotal = "hermodr.pipeline.total";

        // Subscription domain
        /// <summary>Metric name for the subscription handle operation duration.</summary>
        public const string MetricSubscriptionHandleDuration = "hermodr.subscription.handle.duration";
        /// <summary>Metric name for the count of matched subscriptions.</summary>
        public const string MetricSubscriptionMatched = "hermodr.subscription.matched";
        /// <summary>Metric name for the number of subscription handling errors.</summary>
        public const string MetricSubscriptionErrors = "hermodr.subscription.errors";

        // Transport spans
        /// <summary>Span name for publishing to the RabbitMQ transport.</summary>
        public const string SpanTransportRabbitMq = "transport.publish.rabbitmq";
        /// <summary>Span name for publishing to the Azure Service Bus transport.</summary>
        public const string SpanTransportServiceBus = "transport.publish.servicebus";
        /// <summary>Span name for publishing through the MassTransit transport.</summary>
        public const string SpanTransportMassTransit = "transport.publish.masstransit";
        /// <summary>Span name for publishing through the webhook transport.</summary>
        public const string SpanTransportWebhook = "transport.publish.webhook";
        /// <summary>Span name for publishing through the Dapr transport.</summary>
        public const string SpanTransportDapr = "transport.publish.dapr";

        /// <summary>Item key used to stash the publish start time for duration metrics.</summary>
        public const string PublishStartTimeItemKey = "Hermodr.Metrics.Publish.StartTime";

        /// <summary>Builds the span name for a publish operation bound to the specified event type.</summary>
        /// <param name="eventType">The event type used to qualify the publisher span name.</param>
        /// <returns>The publisher span name composed of the publish prefix and the event type.</returns>
        public static string PublisherSpanName(string eventType) => $"{PublisherSpanNamePrefix} {eventType}";
        /// <summary>Builds the span name for a consume operation bound to the specified event type.</summary>
        /// <param name="eventType">The event type used to qualify the consumer span name.</param>
        /// <returns>The consumer span name composed of the handle prefix and the event type.</returns>
        public static string ConsumerSpanName(string eventType) => $"{ConsumerSpanNamePrefix} {eventType}";
    }
}
