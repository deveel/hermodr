//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Microsoft.Extensions.Logging;

namespace Hermodr
{
    static partial class LoggerExtensions
    {
        [LoggerMessage(-40001, LogLevel.Error,
            "Subscription '{SubscriptionName}' threw an error handling event '{EventType}'.")]
        public static partial void LogSubscriptionHandlerError(
            this ILogger logger,
            Exception ex,
            string subscriptionName,
            string? eventType);

        [LoggerMessage(400100, LogLevel.Debug,
            "No subscriptions matched event of type '{EventType}'.")]
        public static partial void LogNoMatchingSubscriptions(
            this ILogger logger,
            string? eventType);

        [LoggerMessage(400101, LogLevel.Debug,
            "Dispatching event '{EventType}' to subscription '{SubscriptionName}'.")]
        public static partial void LogDispatching(
            this ILogger logger,
            string? eventType,
            string subscriptionName);

        [LoggerMessage(400102, LogLevel.Debug,
            "Event '{EventType}' handled by subscription '{SubscriptionName}'.")]
        public static partial void LogDispatched(
            this ILogger logger,
            string? eventType,
            string subscriptionName);
    }
}

