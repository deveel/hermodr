//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Microsoft.Extensions.Logging;

namespace Deveel.Events;

static partial class LoggerExtensions
{
    // ── OutboxPublishChannel ──────────────────────────────────────────────────

    [LoggerMessage(31001, LogLevel.Debug, "Saving event of type '{EventType}' to the outbox store")]
    public static partial void LogSavingEventToOutbox(this ILogger logger, string? eventType);

    [LoggerMessage(31002, LogLevel.Debug, "Event of type '{EventType}' saved to the outbox store")]
    public static partial void LogEventSavedToOutbox(this ILogger logger, string? eventType);

    [LoggerMessage(31003, LogLevel.Error, "Error while saving event of type '{EventType}' to the outbox store")]
    public static partial void LogErrorSavingEventToOutbox(this ILogger logger, Exception ex, string? eventType);

    [LoggerMessage(31004, LogLevel.Debug, "Skipping outbox persistence for event of type '{EventType}': relay publish signal detected")]
    public static partial void LogSkippingRelayPublishSignal(this ILogger logger, string? eventType);

    // ── OutboxRelayService ────────────────────────────────────────────────────

    [LoggerMessage(31010, LogLevel.Information, "Outbox relay service started (interval: {Interval})")]
    public static partial void LogOutboxRelayStarted(this ILogger logger, TimeSpan interval);

    [LoggerMessage(31011, LogLevel.Debug, "Outbox relay tick: checking for pending messages")]
    public static partial void LogOutboxRelayTick(this ILogger logger);

    [LoggerMessage(31012, LogLevel.Information, "Outbox relay service stopped")]
    public static partial void LogOutboxRelayStopped(this ILogger logger);

    [LoggerMessage(31013, LogLevel.Error, "An unhandled error occurred while processing outbox messages")]
    public static partial void LogErrorProcessingOutboxMessages(this ILogger logger, Exception ex);

    // ── OutboxRelayProcessor ──────────────────────────────────────────────────

    [LoggerMessage(31020, LogLevel.Debug, "No pending outbox messages found")]
    public static partial void LogNoOutboxMessagesPending(this ILogger logger);

    [LoggerMessage(31021, LogLevel.Debug, "Processing {Count} pending outbox message(s)")]
    public static partial void LogProcessingOutboxMessages(this ILogger logger, int count);

    [LoggerMessage(31022, LogLevel.Debug, "Relaying outbox message of type '{EventType}' to transport channels")]
    public static partial void LogRelayingOutboxMessage(this ILogger logger, string? eventType);

    [LoggerMessage(31023, LogLevel.Debug, "Outbox message of type '{EventType}' delivered successfully")]
    public static partial void LogOutboxMessageDelivered(this ILogger logger, string? eventType);

    [LoggerMessage(31024, LogLevel.Error, "Error while relaying outbox message of type '{EventType}'")]
    public static partial void LogErrorRelayingOutboxMessage(this ILogger logger, Exception ex, string? eventType);

    [LoggerMessage(31025, LogLevel.Error, "Error while marking outbox message of type '{EventType}' as failed")]
    public static partial void LogErrorMarkingOutboxMessageFailed(this ILogger logger, Exception ex, string? eventType);
}

