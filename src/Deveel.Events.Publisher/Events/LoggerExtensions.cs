//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Microsoft.Extensions.Logging;

namespace Deveel.Events {
	static partial class LoggerExtensions {
		[LoggerMessage(-30001, LogLevel.Error, "Could not create the event of type '{EventType}'")]
		public static partial void LogEventCreateError(this ILogger logger, Exception ex, Type eventType);

		[LoggerMessage(-30002, LogLevel.Error, "Could not create the event from the factory of type '{FactoryType}'")]
        public static partial void LogEventFactoryError(this ILogger logger, Exception ex, Type factoryType);

        [LoggerMessage(-30003, LogLevel.Error, "Could not publish the event of type '{EventType}' through the channel of type '{ChannelType}'")]
		public static partial void LogEventPublishError(this ILogger logger, Exception ex, string eventType, Type channelType);

		[LoggerMessage(400012, LogLevel.Debug, "Publishing an event of type '{EventType}' through the channel of type '{ChannelType}'")]
		public static partial void TraceEventPublishing(this ILogger logger, string eventType, Type channelType);

		[LoggerMessage(400013, LogLevel.Debug, "The event of type '{EventType}' was successfully published through the channel of type '{ChannelType}'")]
		public static partial void TraceEventPublished(this ILogger logger, string eventType, Type channelType);

		[LoggerMessage(400014, LogLevel.Debug, "Event-publish middleware pipeline built with {MiddlewareCount} middleware component(s)")]
		public static partial void TraceMiddlewarePipelineBuilt(this ILogger logger, int middlewareCount);
	}
}
