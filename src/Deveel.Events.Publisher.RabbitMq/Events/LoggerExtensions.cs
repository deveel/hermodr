//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Microsoft.Extensions.Logging;

namespace Deveel.Events
{
    static partial class LoggerExtensions
    {
        [LoggerMessage(30001, LogLevel.Debug, "Event of type {EventType} to be published")]
        public static partial void TracePublishingEvent(this ILogger logger, string? eventType);

        [LoggerMessage(30002, LogLevel.Error, "Error while publishing an event of type '{EventType}'")]
        public static partial void LogErrorPublishingEvent(this ILogger logger, Exception ex, string? eventType);

        [LoggerMessage(30003, LogLevel.Debug, "RabbitMQ channel created successfully")]
        public static partial void LogChannelCreated(this ILogger logger);

        [LoggerMessage(30004, LogLevel.Warning, "RabbitMQ channel was closed; recreating channel")]
        public static partial void LogChannelRecovery(this ILogger logger);

        [LoggerMessage(30005, LogLevel.Debug, "Publisher confirms enabled on RabbitMQ channel")]
        public static partial void LogPublisherConfirmsEnabled(this ILogger logger);

        [LoggerMessage(30006, LogLevel.Debug, "Broker confirmed delivery of event of type '{EventType}'")]
        public static partial void LogEventConfirmed(this ILogger logger, string? eventType);
    }
}
