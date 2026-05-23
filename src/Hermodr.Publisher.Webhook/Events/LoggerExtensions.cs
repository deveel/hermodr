//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Microsoft.Extensions.Logging;

namespace Hermodr
{
    /// <summary>
    /// Compile-time generated, zero-allocation logging helpers for the webhook publisher.
    /// </summary>
    internal static partial class LoggerExtensions
    {
        // ── Delivery start ──────────────────────────────────────────────────

        /// <summary>Logs that a single CloudEvent is about to be delivered.</summary>
        [LoggerMessage(
            EventId  = 4001,
            Level    = LogLevel.Debug,
            Message  = "Delivering '{EventType}' event (delivery={DeliveryId}, format={Format}, algorithm={Algorithm}) to {Url}")]
        public static partial void LogDeliveringEvent(
            this ILogger logger,
            string       eventType,
            string       deliveryId,
            string       format,
            string       algorithm,
            string       url);

        /// <summary>Logs that a batch of CloudEvents is about to be delivered.</summary>
        [LoggerMessage(
            EventId  = 4002,
            Level    = LogLevel.Debug,
            Message  = "Delivering event batch (delivery={DeliveryId}, count={EventCount}, format={Format}, algorithm={Algorithm}) to {Url}")]
        public static partial void LogDeliveringBatch(
            this ILogger logger,
            string       deliveryId,
            int          eventCount,
            string       format,
            string       algorithm,
            string       url);

        // ── Delivery success ────────────────────────────────────────────────

        /// <summary>Logs a successful webhook delivery.</summary>
        [LoggerMessage(
            EventId  = 4003,
            Level    = LogLevel.Debug,
            Message  = "Delivery {DeliveryId} succeeded (status={StatusCode})")]
        public static partial void LogDeliverySucceeded(
            this ILogger logger,
            string       deliveryId,
            int          statusCode);

        // ── Retry warnings ──────────────────────────────────────────────────

        /// <summary>Logs a transient exception that will be retried by Polly.</summary>
        [LoggerMessage(
            EventId  = 4004,
            Level    = LogLevel.Warning,
            Message  = "Delivery {DeliveryId} attempt {Attempt} failed ({ExceptionMessage}); retrying in {DelayMs}ms")]
        public static partial void LogRetryOnException(
            this ILogger logger,
            string       deliveryId,
            int          attempt,
            string?      exceptionMessage,
            double       delayMs);

        /// <summary>Logs a retryable HTTP status code that will cause Polly to retry.</summary>
        [LoggerMessage(
            EventId  = 4005,
            Level    = LogLevel.Warning,
            Message  = "Delivery {DeliveryId} attempt {Attempt} got retryable status {StatusCode}; retrying in {DelayMs}ms")]
        public static partial void LogRetryOnStatusCode(
            this ILogger logger,
            string       deliveryId,
            int          attempt,
            int          statusCode,
            double       delayMs);

        // ── Terminal errors ─────────────────────────────────────────────────

        /// <summary>Logs that all retry attempts were exhausted after a transient exception.</summary>
        [LoggerMessage(
            EventId  = 4006,
            Level    = LogLevel.Error,
            Message  = "Webhook delivery {DeliveryId} failed after {TotalAttempts} attempt(s)")]
        public static partial void LogDeliveryFailed(
            this ILogger logger,
            string       deliveryId,
            int          totalAttempts);

        /// <summary>Logs a non-retryable HTTP status code returned by the endpoint.</summary>
        [LoggerMessage(
            EventId  = 4007,
            Level    = LogLevel.Error,
            Message  = "Non-retryable failure for delivery {DeliveryId} (status={StatusCode})")]
        public static partial void LogNonRetryableFailure(
            this ILogger logger,
            string       deliveryId,
            int          statusCode);

        /// <summary>Logs that all retry attempts were exhausted after repeated retryable status codes.</summary>
        [LoggerMessage(
            EventId  = 4008,
            Level    = LogLevel.Error,
            Message  = "Webhook delivery {DeliveryId} exhausted all {TotalAttempts} attempt(s) with retryable status codes")]
        public static partial void LogDeliveryExhausted(
            this ILogger logger,
            string       deliveryId,
            int          totalAttempts);
    }
}
