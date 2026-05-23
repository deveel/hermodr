//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//
using Microsoft.Extensions.Logging;
namespace Hermodr
{
    internal static partial class LoggerExtensions
    {
        [LoggerMessage(EventId = 1, Level = LogLevel.Trace, Message = "Publishing event of type '{EventType}' via MassTransit")]
        public static partial void TracePublishingEvent(this ILogger logger, string? eventType);
        [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "Error publishing event of type '{EventType}' via MassTransit")]
        public static partial void LogErrorPublishingEvent(this ILogger logger, Exception ex, string? eventType);
    }
}
