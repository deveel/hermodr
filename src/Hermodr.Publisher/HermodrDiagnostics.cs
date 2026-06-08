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
    /// <see cref="OpenTelemetry.EventPublisherBuilderExtensions.UseOpenTelemetry"/>
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
        public const string EventType = "event.type";
        public const string MessagingSystem = "messaging.system";
        public const string MessagingOperation = "messaging.operation";
        public const string MessagingDestination = "messaging.destination";
        public const string MessagingDestinationKind = "messaging.destination_kind";
        public const string MessagingRabbitMqRoutingKey = "messaging.rabbitmq.routing_key";
        public const string HttpRequestMethod = "http.request.method";
        public const string SubscriptionName = "subscription.name";
        public const string ChannelType = "channel.type";
        public const string MessageId = "message.id";
        public const string Success = "success";
    }
}
