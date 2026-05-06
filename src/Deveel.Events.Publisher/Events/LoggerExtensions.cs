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

		[LoggerMessage(400015, LogLevel.Trace, "Building event-publish middleware pipeline")]
		public static partial void TracePipelineBuildStarted(this ILogger logger);

		[LoggerMessage(400016, LogLevel.Trace, "Composing middleware '{MiddlewareType}' at position {Position} into the pipeline")]
		public static partial void TraceMiddlewareComposed(this ILogger logger, Type middlewareType, int position);

		[LoggerMessage(400017, LogLevel.Trace, "Invoking middleware '{MiddlewareType}'")]
		public static partial void TraceMiddlewareInvoking(this ILogger logger, Type middlewareType);

		[LoggerMessage(400018, LogLevel.Trace, "Middleware '{MiddlewareType}' completed")]
		public static partial void TraceMiddlewareCompleted(this ILogger logger, Type middlewareType);

		[LoggerMessage(400019, LogLevel.Trace, "Middleware '{MiddlewareType}' faulted")]
		public static partial void TraceMiddlewareFaulted(this ILogger logger, Exception ex, Type middlewareType);

		[LoggerMessage(400020, LogLevel.Trace, "Executing event-publish pipeline for event of type '{EventType}'")]
		public static partial void TracePipelineExecuting(this ILogger logger, string? eventType);

 		[LoggerMessage(400021, LogLevel.Trace, "Event-publish pipeline completed for event of type '{EventType}'")]
 		public static partial void TracePipelineCompleted(this ILogger logger, string? eventType);

        [LoggerMessage(-30004, LogLevel.Error, "Could not handle the publish error at stage '{Stage}' for the event of type '{EventType}' through the channel of type '{ChannelType}'")]
        public static partial void LogPublishErrorHandlerError(this ILogger logger, Exception ex, EventPublishStage stage, string? eventType, Type? channelType);
 	}
}
