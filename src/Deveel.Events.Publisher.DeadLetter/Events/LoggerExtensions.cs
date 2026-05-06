//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Microsoft.Extensions.Logging;

namespace Deveel.Events;

static partial class LoggerExtensions
{
    [LoggerMessage(32001, LogLevel.Debug, "Saving dead-letter event of type '{EventType}' with id '{MessageId}'")]
    public static partial void LogSavingDeadLetterEvent(this ILogger logger, string? eventType, string? messageId);

    [LoggerMessage(32002, LogLevel.Debug, "Dead-letter event of type '{EventType}' with id '{MessageId}' saved")]
    public static partial void LogDeadLetterEventSaved(this ILogger logger, string? eventType, string? messageId);

    [LoggerMessage(32003, LogLevel.Debug, "Replaying dead-letter event of type '{EventType}' with id '{MessageId}'")]
    public static partial void LogReplayingDeadLetterEvent(this ILogger logger, string? eventType, string? messageId);

    [LoggerMessage(32004, LogLevel.Debug, "Dead-letter event of type '{EventType}' with id '{MessageId}' replayed successfully")]
    public static partial void LogDeadLetterEventReplayed(this ILogger logger, string? eventType, string? messageId);

    [LoggerMessage(32005, LogLevel.Error, "Error while replaying dead-letter event of type '{EventType}' with id '{MessageId}'")]
    public static partial void LogErrorReplayingDeadLetterEvent(this ILogger logger, Exception ex, string? eventType, string? messageId);

    [LoggerMessage(32006, LogLevel.Information, "Dead-letter replay service started (interval: {Interval})")]
    public static partial void LogDeadLetterReplayServiceStarted(this ILogger logger, TimeSpan interval);

    [LoggerMessage(32007, LogLevel.Debug, "Dead-letter replay service tick: checking for pending messages")]
    public static partial void LogDeadLetterReplayServiceTick(this ILogger logger);

    [LoggerMessage(32008, LogLevel.Information, "Dead-letter replay service stopped")]
    public static partial void LogDeadLetterReplayServiceStopped(this ILogger logger);

    [LoggerMessage(32009, LogLevel.Error, "An unhandled error occurred while running the dead-letter replay service")]
    public static partial void LogErrorRunningDeadLetterReplayService(this ILogger logger, Exception ex);

    [LoggerMessage(32010, LogLevel.Debug, "No pending dead-letter messages found")]
    public static partial void LogNoPendingDeadLetterMessages(this ILogger logger);

    [LoggerMessage(32011, LogLevel.Debug, "Processing {Count} pending dead-letter message(s)")]
    public static partial void LogProcessingPendingDeadLetterMessages(this ILogger logger, int count);

    [LoggerMessage(32012, LogLevel.Error, "Error while processing dead-letter event of type '{EventType}' with id '{MessageId}'")]
    public static partial void LogErrorProcessingDeadLetterMessage(this ILogger logger, Exception ex, string? eventType, string? messageId);
}
