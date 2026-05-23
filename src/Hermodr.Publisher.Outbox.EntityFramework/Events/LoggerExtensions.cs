//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Microsoft.Extensions.Logging;

namespace Hermodr;

static partial class LoggerExtensions
{
    [LoggerMessage(32001, LogLevel.Debug, "Marking outbox message as Sending")]
    public static partial void LogMarkingOutboxMessageAsSending(this ILogger logger);

    [LoggerMessage(32002, LogLevel.Debug, "Marking outbox message as Delivered")]
    public static partial void LogMarkingOutboxMessageAsDelivered(this ILogger logger);

    [LoggerMessage(32003, LogLevel.Debug, "Scheduling outbox message retry at {NextRetryAt} (attempt {RetryCount})")]
    public static partial void LogSchedulingOutboxMessageRetry(this ILogger logger, DateTimeOffset nextRetryAt, int retryCount);

    [LoggerMessage(32004, LogLevel.Debug, "Marking outbox message as Failed: {ErrorMessage}")]
    public static partial void LogMarkingOutboxMessageAsFailed(this ILogger logger, string errorMessage);

    [LoggerMessage(32005, LogLevel.Debug, "Retrieved {Count} pending outbox message(s)")]
    public static partial void LogRetrievedPendingOutboxMessages(this ILogger logger, int count);
}

