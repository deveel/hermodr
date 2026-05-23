//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Microsoft.Extensions.Logging;

namespace Hermodr
{
    internal static class LoggerExtensions
    {
        private static readonly Action<ILogger, string, Exception?> _tracePublishSpanNotEnabled =
            LoggerMessage.Define<string>(
                LogLevel.Trace,
                new EventId(1, "TracePublishSpanNotEnabled"),
                "OpenTelemetry publish span not active for event type '{EventType}' (no listener configured)");

        private static readonly Action<ILogger, string, Exception?> _traceConsumeSpanNotEnabled =
            LoggerMessage.Define<string>(
                LogLevel.Trace,
                new EventId(2, "TraceConsumeSpanNotEnabled"),
                "OpenTelemetry consume span not active for event type '{EventType}' (no listener configured)");

        private static readonly Action<ILogger, string, string, Exception?> _traceExtracted =
            LoggerMessage.Define<string, string>(
                LogLevel.Trace,
                new EventId(3, "TraceExtracted"),
                "Extracted trace context from event type '{EventType}' with trace ID '{TraceId}'");

        public static void TracePublishSpanNotEnabled(this ILogger logger, string eventType) =>
            _tracePublishSpanNotEnabled(logger, eventType, null);

        public static void TraceConsumeSpanNotEnabled(this ILogger logger, string eventType) =>
            _traceConsumeSpanNotEnabled(logger, eventType, null);

        public static void TraceExtracted(this ILogger logger, string eventType, string traceId) =>
            _traceExtracted(logger, eventType, traceId, null);
    }
}
