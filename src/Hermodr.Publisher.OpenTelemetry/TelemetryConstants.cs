//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr
{
    /// <summary>
    /// Constants used by the OpenTelemetry instrumentation middleware.
    /// </summary>
    internal static class TelemetryConstants
    {
        public const string DefaultActivitySourceName = "Hermodr";

        public const string TraceParentExtensionName = "traceparent";
        public const string TraceStateExtensionName = "tracestate";

        public const string PublisherSpanNamePrefix = "publish";
        public const string ConsumerSpanNamePrefix = "handle";

        public const string ActivityItemKey = "Hermodr.Activity";

        public const string MessagingSystem = "hermodr";

        public static string PublisherSpanName(string eventType) => $"{PublisherSpanNamePrefix} {eventType}";
        public static string ConsumerSpanName(string eventType) => $"{ConsumerSpanNamePrefix} {eventType}";
    }
}
