//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Diagnostics;

using CloudNative.CloudEvents;

namespace Hermodr
{
    /// <summary>
    /// Provides shared telemetry utilities for Hermodr OpenTelemetry instrumentation.
    /// </summary>
    public static class HermodrTelemetry
    {
        private static ActivitySource? _activitySource;

        /// <summary>
        /// Gets or creates the shared <see cref="ActivitySource"/> for Hermodr instrumentation.
        /// </summary>
        public static ActivitySource ActivitySource
        {
            get => _activitySource ??= new ActivitySource(TelemetryConstants.DefaultActivitySourceName);
            set => _activitySource = value;
        }

        /// <summary>
        /// Injects W3C trace context from the current <see cref="Activity"/> into the
        /// <see cref="CloudEvent"/> as extension attributes.
        /// </summary>
        /// <param name="event">The CloudEvent to enrich.</param>
        /// <param name="activity">The activity whose trace context to inject.</param>
        public static void InjectTraceContext(CloudEvent @event, Activity activity)
        {
            ArgumentNullException.ThrowIfNull(@event);
            ArgumentNullException.ThrowIfNull(activity);

            var traceparent = $"00-{activity.TraceId.ToHexString()}-{activity.SpanId.ToHexString()}-{(activity.ActivityTraceFlags.HasFlag(ActivityTraceFlags.Recorded) ? "01" : "00")}";
            var traceparentAttr = CloudEventAttribute.CreateExtension(TelemetryConstants.TraceParentExtensionName, CloudEventAttributeType.String);
            @event[traceparentAttr] = traceparent;

            if (!string.IsNullOrEmpty(activity.TraceStateString))
            {
                var traceStateAttr = CloudEventAttribute.CreateExtension(TelemetryConstants.TraceStateExtensionName, CloudEventAttributeType.String);
                @event[traceStateAttr] = activity.TraceStateString;
            }
        }

        /// <summary>
        /// Extracts W3C trace context from CloudEvent extension attributes.
        /// </summary>
        /// <param name="event">The CloudEvent to extract from.</param>
        /// <param name="parentContext">The parsed <see cref="ActivityContext"/>, or null if no traceparent was found.</param>
        /// <returns>True if a traceparent extension was found and parsed successfully.</returns>
        public static bool TryExtractTraceContext(CloudEvent @event, out ActivityContext parentContext)
        {
            ArgumentNullException.ThrowIfNull(@event);
            parentContext = default;

            var traceparentAttr = CloudEventAttribute.CreateExtension(TelemetryConstants.TraceParentExtensionName, CloudEventAttributeType.String);
            if (@event[traceparentAttr] is not string traceparent || string.IsNullOrEmpty(traceparent))
            {
                return false;
            }

            var parts = traceparent.Split('-');
            if (parts.Length < 4)
            {
                return false;
            }

            ActivityTraceId traceId;
            ActivitySpanId spanId;

            try
            {
                traceId = ActivityTraceId.CreateFromString(parts[1].AsSpan());
                spanId = ActivitySpanId.CreateFromString(parts[2].AsSpan());
            }
            catch (FormatException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
            var traceFlags = (ActivityTraceFlags)byte.Parse(parts[3], System.Globalization.NumberStyles.HexNumber);

            var traceStateAttr = CloudEventAttribute.CreateExtension(TelemetryConstants.TraceStateExtensionName, CloudEventAttributeType.String);
            var traceState = @event[traceStateAttr] as string;

            parentContext = new ActivityContext(traceId, spanId, traceFlags, traceState, isRemote: true);
            return true;
        }

        /// <summary>
        /// Creates a producer <see cref="Activity"/> for publishing an event.
        /// </summary>
        public static Activity? CreatePublisherActivity(ActivitySource activitySource, string eventType)
        {
            return activitySource.StartActivity(
                TelemetryConstants.PublisherSpanName(eventType),
                ActivityKind.Producer,
                parentContext: default,
                tags: new ActivityTagsCollection
                {
                    ["event.type"] = eventType,
                    ["messaging.system"] = TelemetryConstants.MessagingSystem,
                    ["messaging.operation"] = "publish",
                });
        }

        /// <summary>
        /// Creates a consumer <see cref="Activity"/> for handling an event, optionally linked to a remote parent.
        /// </summary>
        public static Activity? CreateConsumerActivity(ActivitySource activitySource, string eventType, ActivityContext? parentContext = null)
        {
            return activitySource.StartActivity(
                TelemetryConstants.ConsumerSpanName(eventType),
                ActivityKind.Consumer,
                parentContext: parentContext ?? default,
                tags: new ActivityTagsCollection
                {
                    ["event.type"] = eventType,
                    ["messaging.system"] = TelemetryConstants.MessagingSystem,
                    ["messaging.operation"] = "receive",
                });
        }
    }
}
