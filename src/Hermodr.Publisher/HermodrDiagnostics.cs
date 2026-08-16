//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Hermodr
{
    /// <summary>
    /// Shared <see cref="ActivitySource"/> and <see cref="Meter"/> instances for all
    /// Hermodr instrumentation.  The DI registration in
    /// <c>UseOpenTelemetry</c> (Hermodr.Publisher.OpenTelemetry)
    /// replaces these with user-configured instances.
    /// </summary>
    public static class HermodrDiagnostics
    {
        private static readonly object _lock = new();

        private static ActivitySource? _activitySource;
        private static Meter? _meter;

        /// <summary>
        /// Gets or sets the shared <see cref="ActivitySource"/> used by all
        /// Hermodr instrumentation across packages.
        /// Defaults to a new source named <c>"Hermodr"</c>.
        /// </summary>
        public static ActivitySource ActivitySource
        {
            get
            {
                if (_activitySource == null)
                {
                    lock (_lock)
                    {
                        _activitySource ??= new ActivitySource("Hermodr");
                    }
                }
                return _activitySource;
            }
            set
            {
                lock (_lock)
                {
                    _activitySource = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the shared <see cref="Meter"/> used by all Hermodr
        /// instrumentation across packages.
        /// Defaults to a new meter named <c>"Hermodr"</c>.
        /// </summary>
        public static Meter Meter
        {
            get
            {
                if (_meter == null)
                {
                    lock (_lock)
                    {
                        _meter ??= new Meter("Hermodr");
                    }
                }
                return _meter;
            }
            set
            {
                lock (_lock)
                {
                    _meter = value;
                }
            }
        }
    }

    /// <summary>
    /// Constants for OpenTelemetry tag keys used throughout Hermodr instrumentation.
    /// </summary>
    public static class TelemetryTags
    {
        /// <summary>The OpenTelemetry tag key carrying the event type.</summary>
        public const string EventType = "event.type";
        /// <summary>The OpenTelemetry tag key for the messaging system identifier.</summary>
        public const string MessagingSystem = "messaging.system";
        /// <summary>The OpenTelemetry tag key for the messaging operation name.</summary>
        public const string MessagingOperation = "messaging.operation";
        /// <summary>The OpenTelemetry tag key for the messaging destination name.</summary>
        public const string MessagingDestination = "messaging.destination";
        /// <summary>The OpenTelemetry tag key for the messaging destination kind.</summary>
        public const string MessagingDestinationKind = "messaging.destination_kind";
        /// <summary>The OpenTelemetry tag key for the RabbitMQ routing key.</summary>
        public const string MessagingRabbitMqRoutingKey = "messaging.rabbitmq.routing_key";
        /// <summary>The OpenTelemetry tag key for the HTTP request method.</summary>
        public const string HttpRequestMethod = "http.request.method";
        /// <summary>The OpenTelemetry tag key for the subscription name.</summary>
        public const string SubscriptionName = "subscription.name";
        /// <summary>The OpenTelemetry tag key for the channel type.</summary>
        public const string ChannelType = "channel.type";
        /// <summary>The OpenTelemetry tag key for the message identifier.</summary>
        public const string MessageId = "message.id";
        /// <summary>The OpenTelemetry tag key indicating whether the operation succeeded.</summary>
        public const string Success = "success";
    }
}
