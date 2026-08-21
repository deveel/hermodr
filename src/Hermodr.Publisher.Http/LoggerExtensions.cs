//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Logging;

namespace Hermodr
{
    /// <summary>
    /// Compile-time generated, zero-allocation logging helpers for the HTTP publisher.
    /// </summary>
    [ExcludeFromCodeCoverage]
    internal static partial class LoggerExtensions
    {
        // ── Delivery start ──────────────────────────────────────────────────

        /// <summary>Logs that a CloudEvent is about to be delivered to a single endpoint.</summary>
        [LoggerMessage(
            EventId  = 5001,
            Level    = LogLevel.Debug,
            Message  = "Delivering '{EventType}' event (format={Format}) to {Url}")]
        public static partial void LogDeliveringEvent(
            this ILogger logger,
            string       eventType,
            string       format,
            string       url);

        // ── Delivery success ────────────────────────────────────────────────

        /// <summary>Logs a successful HTTP delivery.</summary>
        [LoggerMessage(
            EventId  = 5002,
            Level    = LogLevel.Debug,
            Message  = "Delivery to {Url} succeeded (status={StatusCode})")]
        public static partial void LogDeliverySucceeded(
            this ILogger logger,
            string       url,
            int          statusCode);

        // ── Terminal errors ─────────────────────────────────────────────────

        /// <summary>Logs that all retry attempts were exhausted after a transient exception.</summary>
        [LoggerMessage(
            EventId  = 5003,
            Level    = LogLevel.Error,
            Message  = "HTTP delivery to {Url} failed after {TotalAttempts} attempt(s)")]
        public static partial void LogDeliveryFailed(
            this ILogger logger,
            string       url,
            int          totalAttempts);

        /// <summary>Logs a non-retryable HTTP status code returned by the endpoint.</summary>
        [LoggerMessage(
            EventId  = 5004,
            Level    = LogLevel.Error,
            Message  = "Non-retryable failure for delivery to {Url} (status={StatusCode})")]
        public static partial void LogNonRetryableFailure(
            this ILogger logger,
            string       url,
            int          statusCode);

        /// <summary>Logs that all retry attempts were exhausted after repeated retryable status codes.</summary>
        [LoggerMessage(
            EventId  = 5005,
            Level    = LogLevel.Error,
            Message  = "HTTP delivery to {Url} exhausted all {TotalAttempts} attempt(s) with retryable status codes")]
        public static partial void LogDeliveryExhausted(
            this ILogger logger,
            string       url,
            int          totalAttempts);

        /// <summary>Logs that a fan-out publish failed for some endpoints.</summary>
        [LoggerMessage(
            EventId  = 5006,
            Level    = LogLevel.Error,
            Message  = "HTTP publish failed for {FailedCount} of {TotalEndpoints} endpoint(s)")]
        public static partial void LogPublishFailed(
            this ILogger logger,
            int       failedCount,
            int       totalEndpoints);
    }
}