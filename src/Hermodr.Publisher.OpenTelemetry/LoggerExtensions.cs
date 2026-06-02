//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Logging;

namespace Hermodr
{
    [ExcludeFromCodeCoverage]
    internal static partial class LoggerExtensions
    {
        [LoggerMessage(EventId = 35001, Level = LogLevel.Trace, Message = "OpenTelemetry publish span not active for event type '{EventType}' (no listener configured)")]
        public static partial void TracePublishSpanNotEnabled(this ILogger logger, string eventType);

        [LoggerMessage(EventId = 35002, Level = LogLevel.Trace, Message = "OpenTelemetry consume span not active for event type '{EventType}' (no listener configured)")]
        public static partial void TraceConsumeSpanNotEnabled(this ILogger logger, string eventType);

        [LoggerMessage(EventId = 35003, Level = LogLevel.Trace, Message = "Extracted trace context from event type '{EventType}' with trace ID '{TraceId}'")]
        public static partial void TraceExtracted(this ILogger logger, string eventType, string traceId);

        [LoggerMessage(EventId = 35004, Level = LogLevel.Trace, Message = "Metrics not enabled for event type '{EventType}'")]
        public static partial void MetricsNotEnabled(this ILogger logger, string eventType);
    }
}
