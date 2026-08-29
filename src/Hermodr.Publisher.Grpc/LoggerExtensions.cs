//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Microsoft.Extensions.Logging;

namespace Hermodr
{
    internal static partial class LoggerExtensions
    {
        [LoggerMessage(
            Level = LogLevel.Debug,
            Message = "Delivering event of type '{EventType}' to gRPC endpoint '{Address}' (RPC type: {RpcType}).")]
        public static partial void LogDeliveringEvent(
            this ILogger logger, string? eventType, string? address, string rpcType);

        [LoggerMessage(
            Level = LogLevel.Debug,
            Message = "gRPC delivery to '{Address}' succeeded.")]
        public static partial void LogDeliverySucceeded(
            this ILogger logger, string? address);

        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "gRPC delivery to '{Address}' failed ({FailureCount} failure).")]
        public static partial void LogDeliveryFailed(
            this ILogger logger, string? address, int failureCount);

        [LoggerMessage(
            Level = LogLevel.Error,
            Message = "gRPC publish failed for {FailureCount} of {EndpointCount} endpoint(s).")]
        public static partial void LogPublishFailed(
            this ILogger logger, int failureCount, int endpointCount);

        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "The gRPC publish was cancelled for {CancelledCount} of {EndpointCount} endpoint deliveries while other endpoint failures were being reported.")]
        public static partial void LogDeliveriesCancelledWhileOthersFailed(
            this ILogger logger, int cancelledCount, int endpointCount);

        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "The gRPC endpoint '{Address}' uses plaintext HTTP (no TLS): event payloads are transmitted unencrypted.")]
        public static partial void LogPlaintextDelivery(
            this ILogger logger, string? address);
    }
}
